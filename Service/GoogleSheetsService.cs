using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Discord;
using Discord.WebSocket;
using NTH.Common;

public class GoogleSheetsService
{
    private readonly SheetsService _sheetsService;
    public GoogleSheetsService()
    {
        var credential = GoogleCredential
            .FromFile("sheet-quiz-access-credential.json")
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheetsService = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DiscordVocabBot"
        });
    }


    public async Task<List<T>> ReadValuesAsModelListAsync<T>(string range) where T : new()
    {
        var request = _sheetsService.Spreadsheets.Values.Get(Setting.SPREAD_SHEET_ID, range);

        var response = await request.ExecuteAsync();
        var values = response.Values;

        if (values == null || values.Count == 0)
        {
            return new List<T>();
        }

        // Gọi phương thức extension đã được nhúng
        return SheetExtensions.ToModelList<T>(values);
    }

    /// <summary>
    /// Thêm một đối tượng model vào cuối một sheet Google Sheets.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của model cần thêm.</typeparam>
    /// <param name="model">Đối tượng model cần thêm.</param>
    /// <param name="sheetName">Tên của sheet (ví dụ: "Users", "vocab").</param>
    /// <param name="headerNames">Danh sách các tên cột (header) theo thứ tự trong Google Sheet.
    /// Đây là ĐIỀU CỰC KỲ QUAN TRỌNG để đảm bảo dữ liệu được ghi vào đúng cột.
    /// Ví dụ: new List<string> { "UserId", "UserName", "RemindTime", "DoTestTime", "CreatedDate", "CreateUserId", "IsDeleted" }</param>
    /// <returns>Task.</returns>
    public async Task AppendModelAsync<T>(T model, string sheetName, List<string> headerNames) where T : class
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "Model cannot be null.");
        }
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new ArgumentException("Sheet name cannot be null or empty.", nameof(sheetName));
        }
        if (headerNames == null || !headerNames.Any())
        {
            throw new ArgumentException("Header names list cannot be null or empty. It is required to map model properties to sheet columns.", nameof(headerNames));
        }

        var range = $"{sheetName}!A:Z"; // Sử dụng Z để bao phủ đủ các cột tiềm năng.

        // Chuyển đổi model thành một hàng dữ liệu IList<object> sử dụng SheetExtensions
        // Truyền headerNames để đảm bảo thứ tự cột chính xác.
        var rowValues = model.ToSheetRow(headerNames);

        var valueRange = new ValueRange
        {
            Values = new List<IList<object>> { rowValues }
        };

        var appendRequest = _sheetsService.Spreadsheets.Values.Append(valueRange, Setting.SPREAD_SHEET_ID, range);
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED; // Đảm bảo giữ định dạng dữ liệu (ví dụ: ngày giờ)

        try
        {
            await appendRequest.ExecuteAsync();
        }
        catch (Google.GoogleApiException gae)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Tạo một sheet mới trong spreadsheet với tên và các tiêu đề cột (header) được cung cấp,
    /// HOẶC trả về thuộc tính của sheet hiện có nếu nó đã tồn tại.
    /// </summary>
    /// <param name="sheetName">Tên của sheet mới.</param>
    /// <param name="headers">Danh sách các chuỗi là tiêu đề cột. Sẽ được thêm nếu sheet mới được tạo.</param>
    /// <returns>SheetProperties của sheet được tạo hoặc tìm thấy.</returns>
    public async Task CreateSheetWithHeadersAsync(string sheetName, List<string> headers)
    {
        try
        {
            // Bước 1: Kiểm tra xem sheet đã tồn tại chưa
            var spreadsheet = await _sheetsService.Spreadsheets.Get(Setting.SPREAD_SHEET_ID).ExecuteAsync();
            var existingSheet = spreadsheet.Sheets?.FirstOrDefault(s => s.Properties.Title == sheetName);

            if (existingSheet != null)
            {
                return ; // Trả về thuộc tính của sheet đã tồn tại
            }


            // Bước 2: Nếu chưa tồn tại, tạo sheet mới
            var addSheetRequest = new AddSheetRequest
            {
                Properties = new SheetProperties
                {
                    Title = sheetName
                }
            };

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest();
            batchUpdateRequest.Requests = new List<Request>
            {
                new Request { AddSheet = addSheetRequest }
            };

            var createSheetResponse = await _sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, Setting.SPREAD_SHEET_ID).ExecuteAsync();

            var newSheetProperties = createSheetResponse.Replies[0].AddSheet.Properties;

            // Bước 3: Viết các header vào sheet mới tạo
            if (headers != null && headers.Any())
            {
                var headerRow = new ValueRange
                {
                    Values = new List<IList<object>> { headers.Cast<object>().ToList() }
                };

                var updateRange = $"{sheetName}!A1"; // Bắt đầu từ ô A1
                var appendRequest = _sheetsService.Spreadsheets.Values.Append(headerRow, Setting.SPREAD_SHEET_ID, updateRange);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                appendRequest.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS; // Đảm bảo chèn hàng mới nếu cần

                await appendRequest.ExecuteAsync();
            }

            return;
        }
        catch (Google.GoogleApiException gae)
        {
            throw; // Re-throw các lỗi khác không phải "Sheet name already exists"
        }
        catch (Exception ex)
        {
            throw;
        }
    }


    /// <summary>
    /// Xóa một phạm vi hàng cụ thể khỏi một sheet trong Google Spreadsheet.
    /// </summary>
    /// <param name="sheetName">Tên của sheet cần xóa hàng.</param>
    /// <param name="startRowIndexToDelete">Số hàng bắt đầu (1-based) cần xóa (inclusive).</param>
    /// <param name="endRowIndexToDelete">Số hàng kết thúc (1-based) cần xóa (inclusive).</param>
    /// <returns>Task.</returns>
    public async Task DeleteRowsAsync(string sheetName, int startRowIndexToDelete, int endRowIndexToDelete)
    {
        if (startRowIndexToDelete <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startRowIndexToDelete), "Start row index to delete must be greater than 0.");
        }
        if (endRowIndexToDelete < startRowIndexToDelete)
        {
            throw new ArgumentOutOfRangeException(nameof(endRowIndexToDelete), "End row index to delete must be greater than or equal to start row index.");
        }

        try
        {
            // Bước 1: Lấy Sheet ID (GID) từ tên sheet
            int sheetId = await GetSheetIdFromName(sheetName);

            // Bước 2: Tạo yêu cầu xóa hàng
            var deleteRequest = new DeleteDimensionRequest
            {
                Range = new DimensionRange
                {
                    SheetId = sheetId,
                    Dimension = "ROWS", // Xóa theo hàng
                    StartIndex = startRowIndexToDelete - 1, // API sử dụng chỉ số 0-based
                    EndIndex = endRowIndexToDelete // API sử dụng chỉ số 0-based, EndIndex là độc quyền (exclusive)
                }
            };

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest();
            batchUpdateRequest.Requests = new List<Request>
                {
                    new Request { DeleteDimension = deleteRequest }
                };

            // Bước 3: Thực thi yêu cầu Batch Update
            await _sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, Setting.SPREAD_SHEET_ID).ExecuteAsync();

        }
        catch (Google.GoogleApiException gae)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<int> GetSheetIdFromName(string sheetName)
    {
        var spreadsheet = await _sheetsService.Spreadsheets.Get(Setting.SPREAD_SHEET_ID).ExecuteAsync();
        var targetSheet = spreadsheet.Sheets?.FirstOrDefault(s => s.Properties.Title == sheetName);

        if (targetSheet == null || targetSheet.Properties?.SheetId == null)
        {
            throw new InvalidOperationException($"Sheet '{sheetName}' not found or does not have a valid Sheet ID.");
        }

        return targetSheet.Properties.SheetId.Value;
    }
}
