using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using NTH.Model;
using NTH.Common;

namespace NTH.Service;
public class NotificationService
{
    private readonly GoogleSheetsService _googleSheetsService;
    private readonly CacheService _cacheService;
    private readonly UserService _userService;
    private readonly TestService _testService;
    private readonly ResultService _resultService;
    private readonly LibraryService _libraryService;
    private readonly DiscordSocketClient _client;
    private Timer? _timer = null;


    public NotificationService(
        GoogleSheetsService googleSheetsService,
        CacheService cacheService,
        UserService userService,
        TestService testService,
        ResultService resultService,
        LibraryService libraryService,
        DiscordSocketClient client)
    {
        _googleSheetsService = googleSheetsService;
        _cacheService =  cacheService;
        _userService = userService;
        _testService = testService;
        _resultService = resultService;
        _libraryService = libraryService;
        _client = client;

        _timer = new Timer(NotiRemindAddVocab, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    private async void NotiRemindAddVocab(object? state)
    {
        if (_client.ConnectionState != ConnectionState.Connected || _client.CurrentUser == null)
        {
            return;
        }
        try
        {
            var users = _cacheService.GetFixedData<List<UserModel>>("user");

            if (users == null)
            {
                users = await _userService.GetAllUsers();
            }

            foreach (var userModel in users)
            {
                if (!ulong.TryParse(userModel.UserId, out ulong discordUserId))
                {
                    continue;
                }

                if (_testService.IsOnTesting(discordUserId))
                {
                    continue;
                }

                var currentTime = DateTime.Now.TimeOfDay;
                if (userModel.RemindTime.HasValue)
                {
                    var remindTimeSpan = userModel.RemindTime.Value.ToTimeSpan();
                    TimeSpan timeSinceRemind = currentTime - remindTimeSpan;
                    if (timeSinceRemind.TotalMinutes >= 0 && (int)timeSinceRemind.TotalMinutes % Constant.REMINDER_INTERVAL_MINUTES == 0)
                    {
                        var listVocabs = await _libraryService.GetFilteredListByDate(discordUserId, DateTime.Today.AddDays(1));

                        if (listVocabs == null || listVocabs.Count < 4)
                        {
                            await SendReminderToUser(discordUserId, "add-vocab");
                        }
                    }
                }

                if (userModel.DoTestTime.HasValue)
                {
                    var doTestTimeSpan = userModel.DoTestTime.Value.ToTimeSpan();
                    TimeSpan timeSinceDoTest = currentTime - doTestTimeSpan;
                    if (timeSinceDoTest.TotalMinutes >= 0 && (int)timeSinceDoTest.TotalMinutes % Constant.REMINDER_INTERVAL_MINUTES == 0)
                    {
                        bool hasCompletedTestToday = await _resultService.CheckCompletedTestToday(discordUserId);

                        if (!hasCompletedTestToday)
                        {
                            await SendReminderToUser(discordUserId, "do-test");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    private async Task SendReminderToUser(ulong userId, string notificationType)
    {
        try
        {
            IUser? user = _client.GetUser(userId);

            if (user == null)
            {
                user = await _client.Rest.GetUserAsync(userId);
            }
            
            if (user != null)
            {
                EmbedBuilder embedBuilder = new EmbedBuilder()
                    .WithColor(Color.Gold)
                    .WithTimestamp(DateTimeOffset.Now);

                if (notificationType == "add-vocab")
                {
                    embedBuilder.WithTitle("🔔 Nhắc nhở từ Bot Học Từ Vựng!")
                        .WithDescription("Đã đến giờ để bạn điền từ vào kho từ vựng của mình rồi đó! Hãy sử dụng lệnh `/add-vocab` để thêm từ mới nhé.");
                }
                else if (notificationType == "do-test")
                {
                    embedBuilder.WithTitle("⏰ Nhắc nhở làm bài kiểm tra!")
                        .WithDescription("Đã đến giờ làm bài kiểm tra từ vựng rồi đó! Hãy sử dụng lệnh `/start-test` để bắt đầu nhé.");
                }
                var embed = embedBuilder.Build();

                // Chỉ gửi DM, không cần tìm kênh guild mặc định
                var dmChannel = await user.CreateDMChannelAsync();
                await dmChannel.SendMessageAsync(embed: embed);
            }
            else
            {
                Console.WriteLine("Sent reminder to user ({UserId}) via DM (no suitable guild channel found).", userId);
            }
        }
        catch (Exception ex)
        {
        }
    }
}
