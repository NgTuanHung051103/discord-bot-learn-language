using Discord.WebSocket;
using NTH.Extensions;
using NTH.MappingSheet;
using NTH.Model;
using NTH.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using static System.Collections.Specialized.BitVector32;

namespace NTH.Service
{
    public class TestService
    {
        private LibraryService _libraryService;
        private ResultService _resultService;
        private readonly Dictionary<ulong, UserTestSessionModel> _activeTestSessions = new Dictionary<ulong, UserTestSessionModel>();
        private const double PASS_SCORE_THRESHOLD = 0.70;
        private readonly DiscordSocketClient _client;

        public TestService(
            LibraryService libraryService,
            ResultService resultService,
            DiscordSocketClient client)
        {
            _libraryService = libraryService;
            _resultService = resultService;
            _client = client;
        }

        public async Task StartTest(SocketSlashCommand command)
        {
            await command.DeferAsync();
            var discordUserId = command.User.Id;

            if (_activeTestSessions.ContainsKey(discordUserId) && _activeTestSessions[discordUserId].IsActive)
            {
                await command.ModifyOriginalResponseAsync(msg => { msg.Content = "Bạn đã có một bài kiểm tra đang diễn ra rồi! Vui lòng hoàn thành hoặc sử dụng lệnh `/endtest` để kết thúc."; });
                return;
            }

            var dateOption = command.Data.Options.FirstOrDefault(x => x.Name == "target_date");
            var questionTypeOption = command.Data.Options.FirstOrDefault(x => x.Name == "question_type");

            string questionType = "MeaningToForeignLanguage";
            if (questionTypeOption != null && !string.IsNullOrWhiteSpace(questionTypeOption.Value.ToString()))
            {
                // Discord sẽ gửi giá trị đã chọn (Value) là string, ví dụ "ForeignLanguageToMeaning"
                questionType = questionTypeOption.Value.ToString()!;
            }

            DateTime? targetDate = CommonExtensions.GetDayDefault(dateOption);

            List<VocabModelResponse>? filteredVocabsResponse = await _libraryService.GetFilteredListByDate(discordUserId, targetDate);

            ListExtensions.Shuffle(filteredVocabsResponse);

            var session = new UserTestSessionModel
            {
                UserId = discordUserId,
                VocabListForTest = filteredVocabsResponse,
                CurrentQuestionIndex = 0,
                CorrectAnswersCount = 0,
                SessionStartTime = DateTime.Now,
                QuestionType = questionType,
                ChannelId = command.ChannelId.GetValueOrDefault(),
                IsActive = true,
                DoTestDate = targetDate,
            };

            _activeTestSessions[discordUserId] = session;

            var firstQuestionEmbed = BuildQuestionEmbed(session);
            var message = await command.ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = firstQuestionEmbed;
                msg.Content = null; // Xóa nội dung text nếu chỉ gửi Embed
            });

        }

        private Embed BuildQuestionEmbed(UserTestSessionModel session)
        {
            var currentVocab = session.VocabListForTest[session.CurrentQuestionIndex];
            var embedBuilder = new EmbedBuilder()
                .WithColor(Color.Purple)
                .WithFooter($"Câu hỏi {session.CurrentQuestionIndex + 1}/{session.VocabListForTest.Count} | Trả lời vào đây.")
                .WithTimestamp(DateTimeOffset.Now);

            if (session.QuestionType == "ForeignLanguageToMeaning")
            {
                embedBuilder.WithTitle("❓ Nghĩa của từ này là gì?");
                embedBuilder.WithDescription($"**{currentVocab.ForeignLanguage}**");
            }
            else // MeaningToForeignLanguage
            {
                embedBuilder.WithTitle("❓ Từ tiếng Anh của nghĩa này là gì?");
                embedBuilder.WithDescription($"**{currentVocab.Meaning}**");
            }

            return embedBuilder.Build();
        }

        public bool IsUserInActiveTestSession(ulong userId)
        {
            return _activeTestSessions.ContainsKey(userId) && _activeTestSessions[userId].IsActive;
        }

        public async Task ProcessAnswer(SocketMessage message)
        {
            var userId = message.Author.Id;
            if (!_activeTestSessions.TryGetValue(userId, out UserTestSessionModel? session) || !session.IsActive)
            {
                // Người dùng không có phiên hoạt động, bỏ qua
                return;
            }

            var currentVocab = session.VocabListForTest[session.CurrentQuestionIndex];
            string userAnswer = message.Content.Trim();
            string correctAnswer;

            // Xác định câu trả lời đúng dựa trên loại câu hỏi
            if (session.QuestionType == "ForeignLanguageToMeaning")
            {
                correctAnswer = currentVocab.Meaning?.Trim() ?? string.Empty;
            }
            else // MeaningToForeignLanguage
            {
                correctAnswer = currentVocab.ForeignLanguage?.Trim() ?? string.Empty;
            }

            // So sánh câu trả lời (không phân biệt chữ hoa/thường)
            bool isCorrect = string.Equals(userAnswer, correctAnswer, StringComparison.OrdinalIgnoreCase);

            // Gửi phản hồi cho câu trả lời
            string feedbackMessage;
            if (isCorrect)
            {
                session.CorrectAnswersCount++;
                feedbackMessage = "✅ **Đúng rồi!**";
            }
            else
            {
                feedbackMessage = $"❌ **Sai rồi!** Câu trả lời đúng là: **`{correctAnswer}`**";
            }

            // Gửi phản hồi vào kênh
            await message.Channel.SendMessageAsync(feedbackMessage);

            // Chuyển sang câu hỏi tiếp theo
            session.CurrentQuestionIndex++;

            if (session.CurrentQuestionIndex < session.VocabListForTest.Count)
            {
                // Gửi câu hỏi tiếp theo
                var nextQuestionEmbed = BuildQuestionEmbed(session);
                var nextQuestionMessage = await message.Channel.SendMessageAsync(embed: nextQuestionEmbed);
                session.MessageId = nextQuestionMessage.Id; 
            }
            else
            {
                await EndTest(userId);
            }
        }

        public async Task EndTest(ulong userId, bool forceEnd = false)
        {
            if (!_activeTestSessions.TryGetValue(userId, out UserTestSessionModel? session) || !session.IsActive)
            {
                if (forceEnd)
                {
                    // Nếu buộc kết thúc nhưng không có phiên hoạt động
                    var user = _client.GetUser(userId);
                    if (user != null)
                    {
                        var channelUser = await user.CreateDMChannelAsync();
                        await channelUser.SendMessageAsync("Bạn không có bài kiểm tra nào đang diễn ra để kết thúc.");
                    }
                }
                return;
            }

            double score = (double)session.CorrectAnswersCount / session.VocabListForTest.Count;
            bool passed = score >= PASS_SCORE_THRESHOLD;

            var result = new ResultModel
            {
                DoTestDate = session.DoTestDate,
                IsPassed = passed,
                CreatedDate = session.SessionStartTime,
                CorrectAnswers = session.CorrectAnswersCount,
                TotalVocab = session.VocabListForTest.Count,
            };

            var resultEmbed = BuildResultEmbed(score, passed, session.VocabListForTest.Count, session.CorrectAnswersCount);

            session.IsActive = false;
            _activeTestSessions.Remove(userId);

            // Gửi Embed tổng kết kết quả
            var channel = _client.GetChannel(session.ChannelId) as IMessageChannel;
            if (channel != null)
            {
                await channel.SendMessageAsync(embed: resultEmbed);
            }
            else
            {
                // Nếu không tìm thấy kênh, gửi DM cho người dùng
                var user = _client.GetUser(userId);
                if (user != null)
                {
                    var dmChannel = await user.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync(embed: resultEmbed);
                }
            }
            await _resultService.AddResult(userId, result);
        }

        private Embed BuildResultEmbed(double score, bool passed, int totalVocab, int correctAnswers)
        {
            var embedBuilder = new EmbedBuilder()
                .WithTitle("🎉 Kết quả bài kiểm tra của bạn!")
                .WithDescription($"Bạn đã hoàn thành {totalVocab} câu hỏi.")
                .AddField("Số câu đúng", correctAnswers, true)
                .AddField("Tổng số câu", totalVocab, true)
                .AddField("Tỷ lệ đúng", $"{score:P2}", true)
                .WithTimestamp(DateTimeOffset.Now);

            if (passed)
            {
                embedBuilder.WithColor(Color.Green)
                    .AddField("Trạng thái", "✅ **ĐẠT!** Chúc mừng bạn!", false);
            }
            else
            {
                embedBuilder.WithColor(Color.Red)
                    .AddField("Trạng thái", "❌ **CHƯA ĐẠT!** Cố gắng hơn nhé!", false);
            }

            return embedBuilder.Build();
        }

        public bool IsOnTesting(ulong userId)
        {
            return _activeTestSessions.ContainsKey(userId) && _activeTestSessions[userId].IsActive;
        }
    }
}
