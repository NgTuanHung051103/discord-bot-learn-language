using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NTH.DefineEnum;
using System.Linq;
using NTH.Extensions;
using System.ComponentModel;
using System;
using NTH.Model;
using NTH.Common;
namespace NTH.Service
{
    public class BotService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly LibraryService _libraryService;
        private readonly NotificationService _notificationService;
        private readonly UserService _userSerivce;
        private readonly TestService _testSerivce;

        public BotService(
            DiscordSocketClient client,
            LibraryService libraryService,
            NotificationService notificationService,
            TestService testService,
            UserService userService)
        {
            _client = client;
            _notificationService = notificationService;
            _libraryService = libraryService;
            _userSerivce = userService;
            _testSerivce = testService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {

            _client.Log += OnLog;
            _client.Ready += OnReady;
            _client.SlashCommandExecuted += HandleSlashCommand;
            _client.MessageReceived += HandleMessageReceived;
            _client.AutocompleteExecuted += HandleAutocomplete;

            await _userSerivce.SetCacheUsers();
            await _client.LoginAsync(TokenType.Bot, Setting.DISCORD_BOT_TOKEN);
            await _client.StartAsync();

            //_notificationService.ScheduleDailyPing(() =>
            //{
            //    // Gửi @everyone 7h sáng
            //});
        }

        private async Task HandleMessageReceived(SocketMessage message)
        {
            // Bỏ qua tin nhắn từ bot
            if (message.Author.IsBot || message.Channel is SocketDMChannel) return;

            if (_testSerivce.IsUserInActiveTestSession(message.Author.Id))
            {
                // Gọi phương thức xử lý câu trả lời trong VocabService
                await _testSerivce.ProcessAnswer(message);
            }
        }
        private Task OnLog(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task OnReady()
        {
            var user = new SlashCommandBuilder()
              .WithName("user")
              .WithDescription("Xem danh sách người dùng");

            var register = new SlashCommandBuilder()
                .WithName("register")
                .WithDescription("Đăng ký tài khoản của bạn để sử dụng các tính năng của bot.")
                .AddOption("remind_time", ApplicationCommandOptionType.String,
                           "Thời gian nhắc nhở hàng ngày (ví dụ: 08:30)", isRequired: true)
                .AddOption("dotest_time", ApplicationCommandOptionType.String,
                           "Thời gian làm bài hàng ngày (ví dụ: 20:00)", isRequired: true);

            var add = new SlashCommandBuilder()
                .WithName("add")
                .WithDescription("Thêm từ vựng")
                .AddOption("vietnamese_word", ApplicationCommandOptionType.String, "Nghĩa tiếng Việt", true)
                .AddOption("english_word", ApplicationCommandOptionType.String, "Từ tiếng Anh", true);

            var getMyVocabCommand = new SlashCommandBuilder()
                .WithName("get-my-vocab")
                .WithDescription("Xem danh sách từ vựng cần kiểm tra theo ngày từ sheet riêng của bạn.")
                .AddOption("target_date", ApplicationCommandOptionType.String,
                           "Ngày cần kiểm tra (YYYY-MM-DD, mặc định là ngày mai)", isRequired: false);


            var addVocab = new SlashCommandBuilder()
                .WithName("add-vocab")
                .WithDescription("Thêm từ vựng")
                .AddOption("foreign_language", ApplicationCommandOptionType.String, "Tiếng nước ngoài", isRequired: true)
                .AddOption("meaning", ApplicationCommandOptionType.String, "Nghĩa", isRequired: true)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("type_word")
                    .WithDescription("Loại từ (noun, verb, adjective,...)")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(false)
                    .WithAutocomplete(true)) // bật gợi ý autocomplete
                .AddOption("pronounce", ApplicationCommandOptionType.String, "Phát âm", isRequired: false)
                .AddOption("description", ApplicationCommandOptionType.String, "Mô tả", isRequired: false)
                .AddOption("do_test_date", ApplicationCommandOptionType.String, "Ngày ôn tập (yyyy-MM-dd)", isRequired: false);

            var deleteVocab = new SlashCommandBuilder()
                .WithName("delete-vocab")
                .WithDescription("Xóa một hoặc nhiều từ vựng khỏi danh sách của bạn theo ID.")
                .AddOption("start_id", ApplicationCommandOptionType.Integer,
                           "ID của từ vựng bắt đầu xóa (bắt buộc).", isRequired: true)
                .AddOption("end_id", ApplicationCommandOptionType.Integer,
                           "ID của từ vựng kết thúc xóa (tùy chọn). Nếu không nhập, chỉ xóa 1 dòng.", isRequired: false);

            var startTest = new SlashCommandBuilder()
                .WithName("start-test")
                .WithDescription("Bắt đầu bài kiểm tra từ vựng tự luận.")
                .AddOption("target_date", ApplicationCommandOptionType.String,
                           "Ngày kiểm tra từ vựng (YYYY-MM-DD, mặc định là hôm nay).", isRequired: false)
                 .AddOption(new SlashCommandOptionBuilder()
                    .WithName("question_type")
                    .WithDescription("Loại câu hỏi (mặc định: Từ ngoại ngữ -> Nghĩa) hoặc ngược lại")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(false)
                    .WithAutocomplete(true));

            var endTestCommand = new SlashCommandBuilder()
                .WithName("end-test")
                .WithDescription("Kết thúc bài kiểm tra từ vựng đang diễn ra.");

            try
            {
                await _client.CreateGlobalApplicationCommandAsync(add.Build());
                await _client.CreateGlobalApplicationCommandAsync(getMyVocabCommand.Build());
                await _client.CreateGlobalApplicationCommandAsync(user.Build());
                await _client.CreateGlobalApplicationCommandAsync(register.Build());
                await _client.CreateGlobalApplicationCommandAsync(addVocab.Build());
                await _client.CreateGlobalApplicationCommandAsync(deleteVocab.Build());
                await _client.CreateGlobalApplicationCommandAsync(startTest.Build());
                await _client.CreateGlobalApplicationCommandAsync(endTestCommand.Build());
                Console.WriteLine("✅ Đã đăng ký slash command.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Lỗi đăng ký slash command: " + ex.Message);
            }
        }

        private async Task HandleSlashCommand(SocketSlashCommand command)
        {
            try
            {
                switch (command.CommandName)  
                {
                    case "user":
                        await _userSerivce.GetUsers(command);
                        break;
                    case "register":
                        await _userSerivce.RegisterUser(command);
                        break;
                    case "vocab":
                        await _libraryService.GetLibrary(command);
                        break;
                    case "add-vocab":
                        await _libraryService.AddWord(command);
                        break;
                    case "get-my-vocab":
                        await _libraryService.GetLibrary(command);
                        break;
                    case "delete-vocab":
                        await _libraryService.DeleteWord(command);
                        break;
                    case "start-test":
                        await _testSerivce.StartTest(command);
                        break;
                    case "end-test":
                        await _testSerivce.EndTest(command.User.Id, true); // True để buộc kết thúc
                        await command.ModifyOriginalResponseAsync(msg => { msg.Content = "Bài kiểm tra của bạn đã được kết thúc."; });
                        break;
                    default:
                        await command.RespondAsync("Lệnh không hợp lệ.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi SlashCommand: {ex.Message}");
            }
        }

        private async Task HandleAutocomplete(SocketAutocompleteInteraction interaction)
        {
            List<AutocompleteResult> suggestions = new List<AutocompleteResult>();
            if (interaction.Data.CommandName == "add-vocab")
            {
                suggestions = Enum.GetValues(typeof(TypeWord))
               .Cast<TypeWord>()
               .Select(e =>
               {
                   var desc = e.GetEnumDescription();

                   return new AutocompleteResult(desc, e.ToString());
               })
               .ToList();
            }
            else if (interaction.Data.CommandName == "start-test")
            {
                suggestions.Add(new AutocompleteResult("Từ ngoại ngữ -> Nghĩa", "ForeignLanguageToMeaning"));
                suggestions.Add(new AutocompleteResult("Nghĩa -> Từ ngoại ngữ", "MeaningToForeignLanguage"));
            }


            if (suggestions.Any())
            {
                await interaction.RespondAsync(suggestions.Take(25));
            }
        }
    }
}
