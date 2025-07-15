using Discord;
using Discord.WebSocket;
using Google.Apis.Sheets.v4.Data;
using NTH.Common;
using NTH.DefineEnum;
using NTH.MappingSheet;
using NTH.Model;
using NTH.Extensions;
using System.Globalization;
using System.Text;

namespace NTH.Service
{
    public class LibraryService
    {
        private readonly GoogleSheetsService _googleSheetsService;
        private const int MAX_EMBED_FIELD_LENGTH = 1000;
        public LibraryService(
            GoogleSheetsService googleSheetsService
            )
        {
            _googleSheetsService = googleSheetsService;
        }

        public async Task GetLibrary(SocketSlashCommand command)
        {
            await command.DeferAsync();
            var discordUserId = command.User.Id;
            var userVocabSheetName = $"{discordUserId}_vocab"; // Tên sheet của người dùng

            var dateOption = command.Data.Options.FirstOrDefault(x => x.Name == "target_date");

            DateTime? targetDate = CommonExtensions.GetDayDefault(dateOption, false);

            if( targetDate == null)
            {
                await command.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = $"Định dạng date không hợp lệ";
                });
                return;
            }
            try
            {
                List<VocabModelResponse>? filteredVocabsResponse = await GetFilteredListByDate(discordUserId, targetDate);

                if (filteredVocabsResponse == null || !filteredVocabsResponse.Any())
                {
                    await command.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Content = $"📚 Không tìm thấy từ vựng nào có ngày kiểm tra là `{targetDate:yyyy-MM-dd}` trong kho từ vựng của bạn.";
                    });
                    return;
                }

                var embeds = GenerateListVocabEmbed(filteredVocabsResponse, targetDate);

                if (embeds.Any())
                {
                    // ModifyOriginalResponseAsync chỉ chấp nhận một Embed.
                    // Nếu có nhiều hơn 1 Embed, cần dùng FollowupAsync cho các Embed tiếp theo.
                    await command.ModifyOriginalResponseAsync(msg => { msg.Embed = embeds.First(); });

                    foreach (var embed in embeds.Skip(1)) // Gửi các Embed còn lại
                    {
                        await command.FollowupAsync(embed: embed);
                    }
                }
                else
                {
                    await command.ModifyOriginalResponseAsync(msg => { msg.Content = $"📚 Không tìm thấy từ vựng nào có ngày kiểm tra là `{targetDate:yyyy-MM-dd}` trong kho từ vựng của bạn."; });
                }
            }
            catch (Exception ex)
            {
                await command.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = "❌ Đã xảy ra lỗi khi lấy danh sách từ vựng. Vui lòng thử lại sau.";
                });
            }
        }
        public async Task AddWord(SocketSlashCommand command)
        {
            await command.DeferAsync();

            var foreignLanguageOption = command.Data.Options.FirstOrDefault(x => x.Name == "foreign_language");
            var meaningOption = command.Data.Options.FirstOrDefault(x => x.Name == "meaning");
            var typeWordOption = command.Data.Options.FirstOrDefault(x => x.Name == "type_word");
            var pronounceOption = command.Data.Options.FirstOrDefault(x => x.Name == "pronounce");
            var descriptionOption = command.Data.Options.FirstOrDefault(x => x.Name == "description");
            var doTestDateOption = command.Data.Options.FirstOrDefault(x => x.Name == "do_test_date");

            if (foreignLanguageOption == null || string.IsNullOrWhiteSpace(foreignLanguageOption.Value.ToString()))
            {
                await command.FollowupAsync("❌ Vui lòng cung cấp 'Từ/cụm từ tiếng nước ngoài'.", ephemeral: true);
                return;
            }
            if (meaningOption == null || string.IsNullOrWhiteSpace(meaningOption.Value.ToString()))
            {
                await command.FollowupAsync("❌ Vui lòng cung cấp nghĩa.", ephemeral: true);
                return;
            }

            var foreignLanguage = foreignLanguageOption.Value.ToString()!;
            var meaning = meaningOption.Value.ToString()!;

            TypeWord typeWord = TypeWord.Unknow;
            if (typeWordOption != null && !string.IsNullOrWhiteSpace(typeWordOption.Value.ToString()))
            {
                if (Enum.TryParse(typeWordOption.Value.ToString(), out TypeWord parsedTypeWord))
                {
                    typeWord = parsedTypeWord;
                }
            }

            DateTime doTestDate = DateTime.Today.AddDays(1);
            if (doTestDateOption != null && !string.IsNullOrWhiteSpace(doTestDateOption.Value.ToString()))
            {
                if (DateTime.TryParseExact(doTestDateOption.Value.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    doTestDate = parsedDate;
                }
                else
                {
                    await command.FollowupAsync("❌ Định dạng 'Ngày kiểm tra' không hợp lệ. Vui lòng nhập theo định dạng YYYY-MM-DD (ví dụ: `2025-07-30`). Mặc định sẽ là ngày mai.", ephemeral: true);
                }
            }

            var userId = command.User.Id.ToString();

            try
            {
                var newVocab = new VocabModel
                {
                    ForeignLanguage = foreignLanguage,
                    Meaning = meaning,
                    TypeWord = typeWord,
                    Pronounce = pronounceOption?.Value?.ToString(),
                    Description = descriptionOption?.Value?.ToString(),
                    DoTestDate = doTestDate,
                    CreatedDate = DateTime.Now,
                    CreateUserId = command.User.Id.ToString(),
                    IsDeleted = false
                };

                // Gọi phương thức tổng quát để thêm model vào Google Sheet
                await _googleSheetsService.AppendModelAsync(newVocab, $"{userId}_vocab", Constant.HEADER_VOCAB.ToList());

                await command.FollowupAsync($"✅ Đã thêm từ vựng mới: **{foreignLanguage}** - **{meaning}** (Loại: {typeWord.GetEnumDescription()}, Kiểm tra vào: {doTestDate:yyyy-MM-dd}).", ephemeral: false);
            }
            catch (Exception ex)
            {
                await command.FollowupAsync("❌ Đã xảy ra lỗi khi thêm từ vựng. Vui lòng thử lại sau.", ephemeral: true);
            }
        }
        /// <summary>
        /// Xóa một hoặc nhiều từ vựng khỏi sheet riêng của người dùng dựa trên ID (số hàng thực tế).
        /// </summary>
        /// <param name="command">Lệnh Discord Slash Command.</param>
        public async Task DeleteWord(SocketSlashCommand command)
        {
            await command.DeferAsync();

            var userId = command.User.Id.ToString();
            var userVocabSheetName = $"{userId}_vocab";

            var startIdOption = command.Data.Options.FirstOrDefault(x => x.Name == "start_id");
            var endIdOption = command.Data.Options.FirstOrDefault(x => x.Name == "end_id");

            int startRowId;
            int endRowId;

            // Kiểm tra và parse start_id
            if (startIdOption == null || !int.TryParse(startIdOption.Value.ToString(), out startRowId) || startRowId <= 1)
            {
                await command.ModifyOriginalResponseAsync(msg => { msg.Content = "❌ ID bắt đầu không hợp lệ. Vui lòng nhập một số nguyên lớn hơn 1 (số hàng thực tế)."; });
                return;
            }

            // Xử lý end_id
            if (endIdOption != null && !string.IsNullOrWhiteSpace(endIdOption.Value.ToString()))
            {
                if (!int.TryParse(endIdOption.Value.ToString(), out endRowId) || endRowId <= 1)
                {
                    await command.ModifyOriginalResponseAsync(msg => { msg.Content = "❌ ID kết thúc không hợp lệ. Vui lòng nhập một số nguyên lớn hơn 1 (số hàng thực tế)."; });
                    return;
                }

                // Kiểm tra điều kiện end_id phải lớn hơn hoặc bằng start_id
                if (endRowId < startRowId)
                {
                    await command.ModifyOriginalResponseAsync(msg => { msg.Content = "❌ ID kết thúc phải lớn hơn hoặc bằng ID bắt đầu."; });
                    return;
                }
            }
            else
            {
                // Nếu end_id không được cung cấp, mặc định chỉ xóa một dòng (start_id)
                endRowId = startRowId;
            }

            try
            {
                // Gọi GoogleSheetsService để xóa hàng/phạm vi hàng
                await _googleSheetsService.DeleteRowsAsync(userVocabSheetName, startRowId, endRowId);

                string responseMessage;
                if (startRowId == endRowId)
                {
                    responseMessage = $"✅ Đã xóa từ vựng có ID hàng.";
                }
                else
                {
                    responseMessage = $"✅ Đã xóa các từ vựng từ hàng `{startRowId}` đến `{endRowId}`.";
                }
                await command.ModifyOriginalResponseAsync(msg => { msg.Content = responseMessage; });
            }
            catch (Exception ex)
            {
                await command.ModifyOriginalResponseAsync(msg => { msg.Content = $"❌ Đã xảy ra lỗi khi xóa từ vựng. Vui lòng thử lại sau."; });
            }
        }

        public List<Embed> GenerateListVocabEmbed(List<VocabModelResponse> filteredVocabs, DateTime? targetDate)
        {
            var embeds = new List<Embed>();
            var embedBuilder = new EmbedBuilder()
                .WithTitle($"📚 Từ vựng cần kiểm tra: {targetDate:yyyy-MM-dd}")
                .WithDescription($"Từ kho từ điền riêng của bạn")
                .WithColor(Discord.Color.Blue)
                .WithFooter("Sử dụng /add-vocab để thêm từ mới!")
                .WithTimestamp(DateTimeOffset.Now);

            // Build content for the fields
            var currentFieldContent = new StringBuilder();
            int currentVocabCountInField = 0;
            int totalVocabsProcessed = 0;

            // Hàm local để thêm Field vào EmbedBuilder hiện tại
            void AddVocabField(string title, string content, bool inline = false)
            {
                embedBuilder.AddField(title, content, inline);
            }

            for (int i = 0; i < filteredVocabs.Count; i++)
            {
                var vocab = filteredVocabs[i];
                string foreignLanguage = vocab.ForeignLanguage ?? "N/A";
                string meaning = vocab.Meaning ?? "N/A";
                string typeWord = vocab.TypeWord.HasValue ? vocab.TypeWord.GetEnumDescription() ?? vocab.TypeWord.Value.ToString() : "N/A";
                string pronounce = vocab.Pronounce ?? "N/A";

                // Định dạng dòng từ vựng
                string vocabLine = $"`{vocab.RowIndex}.` **{foreignLanguage}** - {meaning} ({typeWord}) [{pronounce}]";

                // Nếu thêm dòng này vào field hiện tại sẽ vượt quá giới hạn
                if (currentFieldContent.Length + vocabLine.Length + Environment.NewLine.Length > MAX_EMBED_FIELD_LENGTH)
                {
                    // Thêm field hiện tại vào EmbedBuilder
                    AddVocabField($"Từ vựng {(totalVocabsProcessed - currentVocabCountInField + 1)}-{totalVocabsProcessed}", currentFieldContent.ToString());

                    // Reset cho field mới
                    currentFieldContent.Clear();
                    currentVocabCountInField = 0;

                    // Nếu Embed đã có quá nhiều fields (tối đa 25), hoặc quá lớn, bắt đầu Embed mới
                    if (embedBuilder.Fields.Count >= 25 || embedBuilder.Length > 5500) // Embed max length is 6000
                    {
                        embeds.Add(embedBuilder.Build()); // Hoàn thành Embed hiện tại
                        embedBuilder = new EmbedBuilder() // Tạo Embed mới
                            .WithTitle($"📚 Từ vựng cần kiểm tra (tiếp): {targetDate:yyyy-MM-dd}")
                            .WithDescription($"Từ kho riêng của bạn")
                            .WithColor(Discord.Color.Blue)
                            .WithFooter("Sử dụng /add-vocab để thêm từ mới!")
                            .WithTimestamp(DateTimeOffset.Now);
                    }
                }

                currentFieldContent.AppendLine(vocabLine);
                currentVocabCountInField++;
                totalVocabsProcessed++;
            }

            // Thêm field cuối cùng nếu còn nội dung
            if (currentFieldContent.Length > 0)
            {
                AddVocabField($"Từ vựng {(totalVocabsProcessed - currentVocabCountInField + 1)}-{totalVocabsProcessed}", currentFieldContent.ToString());
            }

            // Thêm EmbedBuilder cuối cùng vào danh sách
            embeds.Add(embedBuilder.Build());

            return embeds;
        }

        public async Task<List<VocabModelResponse>?> GetFilteredListByDate(ulong userId, DateTime? targetDate)
        {
            var allVocabs = await _googleSheetsService.ReadValuesAsModelListAsync<VocabModel>($"{userId}_vocab");

            if (allVocabs == null || !allVocabs.Any())
            {
                return null;
            }

            List<VocabModelResponse> filteredVocabsResponse = allVocabs
               .Select((v, i) => new VocabModelResponse
               {
                   RowIndex = (i += 2).ToString(),
                   ForeignLanguage = v.ForeignLanguage,
                   Meaning = v.Meaning,
                   TypeWord = v.TypeWord,
                   Pronounce = v.Pronounce,
                   Description = v.Description,
                   DoTestDate = v.DoTestDate,
                   CreatedDate = v.CreatedDate,
                   CreateUserId = v.CreateUserId,
                   IsDeleted = v.IsDeleted
               }).ToList();

            filteredVocabsResponse = filteredVocabsResponse
                .Where(v => v.DoTestDate.HasValue &&
                    v.DoTestDate.Value.Date == targetDate &&
                    !v.IsDeleted.GetValueOrDefault())
               .ToList();

            return filteredVocabsResponse;
        }
    }
}
