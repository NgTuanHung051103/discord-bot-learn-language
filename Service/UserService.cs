using Discord;
using Discord.WebSocket;
using System.Text;
using System.Linq;
using NTH.Model;
using NTH.Service; // Namespace của GoogleSheetsService
using Microsoft.Extensions.Logging;
using NTH.Common;

namespace NTH.Service
{
    public class UserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly GoogleSheetsService _googleSheetsService;
        private readonly CacheService _cacheService;
        private const int MAX_DISCORD_MESSAGE_LENGTH = 1950; // Giới hạn an toàn, ít hơn 2000
        
        public UserService(
            ILogger<UserService> logger,
            GoogleSheetsService googleSheetsService,
            CacheService cacheService
            )
        {
            _logger = logger;
            _googleSheetsService = googleSheetsService;
            _cacheService = cacheService;
        }

        public async Task GetUsers(SocketSlashCommand command)
        {
            await command.DeferAsync();

            try
            {
                var userList = await _googleSheetsService.ReadValuesAsModelListAsync<UserModel>($"{Constant.NAME_SHEET_USER}{Constant.RANGE_SHEET_USER}");

                if (userList == null || !userList.Any())
                {
                    await command.FollowupAsync("❌ Không tìm thấy dữ liệu người dùng nào.");
                    return;
                }

                userList = userList.Where(u => !(u.IsDeleted ?? false)).ToList();

                if (!userList.Any())
                {
                    await command.FollowupAsync("❌ Không tìm thấy người dùng nào hợp lệ sau khi lọc.");
                    return;
                }

                // Tiêu đề
                var currentPart = new StringBuilder();
                currentPart.AppendLine("👥 **Danh sách người dùng:**");
                currentPart.AppendLine(); // Thêm dòng trống để tách tiêu đề và dữ liệu

                var sentMessagesCount = 0; // Đếm số tin nhắn đã gửi

                foreach (var user in userList)
                {
                    string userId = user.UserId ?? "N/A";
                    string userName = user.UserName ?? "N/A";
                    string remindTime = user.RemindTime?.ToString("HH:mm") ?? "N/A";
                    string doTestTime = user.DoTestTime?.ToString("HH:mm") ?? "N/A";
                    string createdDate = user.CreatedDate?.ToString("yyyy-MM-dd") ?? "N/A";

                    // Định dạng dòng người dùng với 5 dấu cách giữa mỗi thông tin
                    // Lưu ý: cách này sẽ không căn cột thẳng hàng nếu các giá trị có độ dài khác nhau.
                    string userLine = $"{userId}     {userName}     {remindTime}     {doTestTime}     {createdDate}";

                    // Kiểm tra nếu thêm dòng này sẽ vượt quá giới hạn
                    if (currentPart.Length + userLine.Length + 1 > MAX_DISCORD_MESSAGE_LENGTH) // +1 cho ký tự xuống dòng
                    {
                        // Gửi tin nhắn hiện tại
                        if (sentMessagesCount == 0)
                        {
                            await command.ModifyOriginalResponseAsync(msg => { msg.Content = currentPart.ToString(); });
                        }
                        else
                        {
                            await command.FollowupAsync(currentPart.ToString());
                        }
                        sentMessagesCount++;

                        // Bắt đầu một phần mới
                        currentPart.Clear();
                        currentPart.AppendLine(userLine); // Thêm dòng người dùng vào phần mới
                    }
                    else
                    {
                        // Thêm dòng người dùng vào phần hiện tại
                        currentPart.AppendLine(userLine);
                    }
                }

                // Gửi phần cuối cùng nếu có dữ liệu còn lại
                if (currentPart.Length > 0) // Đảm bảo có nội dung để gửi
                {
                    if (sentMessagesCount == 0)
                    {
                        await command.ModifyOriginalResponseAsync(msg => { msg.Content = currentPart.ToString(); });
                    }
                    else
                    {
                        await command.FollowupAsync(currentPart.ToString());
                    }
                    sentMessagesCount++;
                }

                _logger.LogInformation("Successfully sent user list in {Count} messages.", sentMessagesCount > 0 ? sentMessagesCount : 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user list from Google Sheets.");
                await command.FollowupAsync("Đã xảy ra lỗi khi lấy danh sách người dùng. Vui lòng thử lại sau.", ephemeral: true);
            }
        }

        public async Task RegisterUser(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);

            var discordUserId = command.User.Id.ToString();
            var discordUserName = command.User.Username;

            // Lấy các giá trị từ options
            var remindTimeOption = command.Data.Options.FirstOrDefault(x => x.Name == "remind_time");
            var doTestTimeOption = command.Data.Options.FirstOrDefault(x => x.Name == "dotest_time");


            TimeOnly parsedRemindTime;
            TimeOnly parsedDoTestTime;

            if (remindTimeOption == null || !TimeOnly.TryParse(remindTimeOption.Value.ToString(), out parsedRemindTime))
            {
                await command.ModifyOriginalResponseAsync(msg => { msg.Content = "❌ Thời gian nhắc nhở không hợp lệ. Vui lòng nhập theo định dạng HH:mm (ví dụ: `08:30`)."; });
                return;
            }

            // Kiểm tra và parse DoTestTime
            if (doTestTimeOption == null || !TimeOnly.TryParse(doTestTimeOption.Value.ToString(), out parsedDoTestTime))
            {
                await command.ModifyOriginalResponseAsync(msg => { msg.Content = "❌ Thời gian làm bài không hợp lệ. Vui lòng nhập theo định dạng HH:mm (ví dụ: `20:00`)."; });
                return;
            }

            try
            {
                var newUser = new UserModel
                {
                    UserId = discordUserId,
                    UserName = discordUserName,
                    RemindTime = parsedRemindTime,
                    DoTestTime = parsedDoTestTime,
                    CreatedDate = DateTime.Now,
                    CreateUserId = discordUserId,
                    IsDeleted = false
                };

                await _googleSheetsService.AppendModelAsync(newUser, Constant.NAME_SHEET_USER, Constant.HEADER_USER);
                await _googleSheetsService.CreateSheetWithHeadersAsync($"{discordUserId}_vocab", Constant.HEADER_VOCAB);
                await _googleSheetsService.CreateSheetWithHeadersAsync($"{discordUserId}_result", Constant.HEADER_RESULT);
                _cacheService.RemoveItemInCache("user");
                await SetCacheUsers();

                await command.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = $"Chúc mừng! Bạn đã đăng ký thành công.\n" +
                                  $"Thời gian nhắc nhở hàng ngày của bạn: `{parsedRemindTime:HH:mm}`\n" +
                                  $"Thời gian làm bài hàng ngày của bạn: `{parsedDoTestTime:HH:mm}`\n";
                });
            }
            catch (Exception ex)
            {
                await command.ModifyOriginalResponseAsync(msg => { msg.Content = $"Đã xảy ra lỗi trong quá trình đăng ký: {ex.Message}. Vui lòng thử lại sau."; });
            }
        }

        public async Task SetCacheUsers()
        {
            var userList = await GetAllUsers();

            _cacheService.AddToCache("user", userList);
        }

        public async Task<List<UserModel>?> GetAllUsers()
        {
            var userList = await _googleSheetsService.ReadValuesAsModelListAsync<UserModel>($"{Constant.NAME_SHEET_USER}{Constant.RANGE_SHEET_USER}");

            userList = userList.Where(u => !(u.IsDeleted ?? false)).ToList();
            
            return userList;
        }
    }
}