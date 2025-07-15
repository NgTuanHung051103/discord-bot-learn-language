using System.Reflection;
using System.ComponentModel;

namespace NTH.Common
{
    public static class SheetExtensions
    {
        /// <summary>
        /// Chuyển đổi dữ liệu từ Google Sheets (dưới dạng danh sách các hàng, mỗi hàng là danh sách các object)
        /// thành một danh sách các đối tượng của kiểu T.
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu của model mục tiêu (ví dụ: VocabModel).</typeparam>
        /// <param name="values">Dữ liệu từ Google Sheets, bao gồm hàng tiêu đề (header).</param>
        /// <returns>Một danh sách các đối tượng kiểu T.</returns>
        public static List<T> ToModelList<T>(this IList<IList<object>> values) where T : new()
        {
            var result = new List<T>();

            if (values == null || values.Count == 0)
            {
                return result;
            }

            // Hàng đầu tiên là header
            var header = values[0].Select(h => h.ToString()).ToList();

            // Lấy tất cả các thuộc tính của model T
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Xây dựng ánh xạ từ tên cột (từ DisplayName) sang PropertyInfo
            var columnPropertyMap = new Dictionary<string, PropertyInfo>();
            foreach (var prop in properties)
            {
                var displayAttribute = prop.GetCustomAttribute<DisplayNameAttribute>();
                if (displayAttribute != null)
                {
                    columnPropertyMap[displayAttribute.DisplayName] = prop;
                }
                else
                {
                    // Nếu không có DisplayName, sử dụng tên thuộc tính làm tên cột
                    columnPropertyMap[prop.Name] = prop;
                }
            }

            // Duyệt qua từng hàng dữ liệu (bỏ qua hàng header)
            for (int rowIndex = 1; rowIndex < values.Count; rowIndex++)
            {
                var rowValues = values[rowIndex];
                var item = new T();

                for (int colIndex = 0; colIndex < header.Count; colIndex++)
                {
                    if (colIndex >= rowValues.Count)
                    {
                        // Nếu hàng không đủ cột so với header, bỏ qua các cột thiếu
                        continue;
                    }

                    var columnName = header[colIndex];
                    if (columnPropertyMap.TryGetValue(columnName!, out var propInfo))
                    {
                        var cellValue = rowValues[colIndex];
                        if (cellValue == null || string.IsNullOrWhiteSpace(cellValue.ToString()))
                        {
                            // Nếu giá trị ô là null hoặc rỗng, gán null cho thuộc tính nullable
                            // hoặc giá trị mặc định cho các kiểu non-nullable (sẽ được xử lý bởi Convert.ChangeType)
                            propInfo.SetValue(item, null);
                            continue;
                        }

                        try
                        {
                            // Chuyển đổi kiểu dữ liệu
                            object? convertedValue = null;
                            var targetType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

                            if (targetType.IsEnum)
                            {
                                // Xử lý Enum: chuyển đổi từ string sang enum
                                var enumValue = System.Enum.Parse(targetType, cellValue.ToString()!, true);
                                convertedValue = enumValue;
                            }
                            else if (targetType == typeof(bool))
                            {
                                if (bool.TryParse(cellValue.ToString(), out bool boolVal))
                                {
                                    convertedValue = boolVal;
                                }
                                else if (int.TryParse(cellValue.ToString(), out int intVal))
                                {
                                    convertedValue = intVal != 0;
                                }
                                else
                                {
                                    convertedValue = false; 
                                }
                            }
                            else if (targetType == typeof(TimeOnly))
                            {
                                if (TimeOnly.TryParse(cellValue.ToString(), out TimeOnly timeVal))
                                {
                                    convertedValue = timeVal;
                                }
                                else
                                {
                                    convertedValue = null;
                                }
                            }
                            else
                            {
                                convertedValue = Convert.ChangeType(cellValue, targetType);
                            }

                            propInfo.SetValue(item, convertedValue);
                        }
                        catch (Exception ex)
                        {
                            // Log lỗi nếu có vấn đề khi chuyển đổi kiểu dữ liệu
                            Console.WriteLine($"Error converting value '{cellValue}' for column '{columnName}' to type '{propInfo.PropertyType.Name}': {ex.Message}");
                            // Bạn có thể chọn bỏ qua hoặc gán giá trị mặc định tùy thuộc vào yêu cầu
                        }
                    }
                }
                result.Add(item);
            }

            return result;
        }

        /// <summary>
        /// Chuyển đổi một đối tượng model thành một danh sách các giá trị object,
        /// sắp xếp theo thứ tự các cột như được định nghĩa bởi DisplayNameAttribute
        /// hoặc tên thuộc tính trong model.
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu của model.</typeparam>
        /// <param name="model">Đối tượng model cần chuyển đổi.</param>
        /// <param name="headerNames">Danh sách các tên cột (header) theo thứ tự mong muốn trong sheet.
        /// Nếu null hoặc rỗng, sẽ cố gắng sắp xếp theo thứ tự thuộc tính trong model.</param>
        /// <returns>Một IList<object> đại diện cho một hàng dữ liệu.</returns>
        public static IList<object> ToSheetRow<T>(this T model, List<string>? headerNames = null)
        {
            var rowValues = new List<object>();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Xây dựng ánh xạ từ tên cột (từ DisplayName) sang PropertyInfo
            var propertyMap = new Dictionary<string, PropertyInfo>();
            foreach (var prop in properties)
            {
                var displayAttribute = prop.GetCustomAttribute<DisplayNameAttribute>();
                if (displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.DisplayName))
                {
                    propertyMap[displayAttribute.DisplayName] = prop;
                }
                else
                {
                    propertyMap[prop.Name] = prop;
                }
            }

            // Nếu không cung cấp headerNames, cố gắng sắp xếp theo thứ tự thuộc tính
            // (Thường không đảm bảo đúng thứ tự cột trong sheet, tốt nhất nên truyền headerNames)
            List<string> orderedColumnNames = headerNames ?? properties.Select(p => p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? p.Name).ToList();

            foreach (var columnName in orderedColumnNames)
            {
                if (propertyMap.TryGetValue(columnName, out var propInfo))
                {
                    var value = propInfo.GetValue(model);
                    // Xử lý các kiểu dữ liệu cụ thể để ghi ra string phù hợp cho Google Sheets
                    if (value is DateTime dateTime)
                    {
                        rowValues.Add(dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    else if (value is TimeOnly timeOnly)
                    {
                        rowValues.Add(timeOnly.ToString("HH:mm:ss"));
                    }
                    else if (value is bool boolean)
                    {
                        rowValues.Add(boolean ? "TRUE" : "FALSE");
                    }
                    else if (value is Enum @enum)
                    {
                        rowValues.Add(@enum.ToString()); // Ghi tên enum ra string
                    }
                    else
                    {
                        rowValues.Add(value?.ToString() ?? ""); // Chuyển đổi sang string, nếu null thì là rỗng
                    }
                }
                else
                {
                    // Nếu không tìm thấy thuộc tính cho tên cột này, thêm một ô rỗng
                    rowValues.Add("");
                }
            }
            return rowValues;
        }
    }
}