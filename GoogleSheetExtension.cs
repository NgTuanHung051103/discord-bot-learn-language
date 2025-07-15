
using System.Collections;
using System.Net;
using System.Reflection;
using Ensign;
using Microsoft.VisualBasic.FileIO;

namespace NTH;

public class GoogleSheetExtension
{
    public readonly string m_google_sheet_file_id;

    public readonly string m_google_sheet_sheet_id;

    private readonly string m_google_api_token;

    private readonly List<Dictionary<string, string>> _csvData;

    private readonly List<string> _titles;

    private string URL => "https://docs.google.com/spreadsheets/d/" + m_google_sheet_file_id + "/export?format=csv" + (string.IsNullOrWhiteSpace(m_google_sheet_sheet_id) ? string.Empty : ("&gid=" + m_google_sheet_sheet_id));

    public int RowCount => _csvData.Count;

    public int ColumnCount => _titles.Count;

    public int TitleRowCount { get; set; } = 1;


    public GoogleSheetExtension(List<Dictionary<string, string>> data)
    {
        _csvData = data;
    }

    public GoogleSheetExtension(List<Dictionary<string, string>> data, List<string> titles)
    {
        _csvData = data;
        _titles = titles;
    }

    public GoogleSheetExtension(string apiToken, string fileId, string sheetId)
    {
        m_google_api_token = apiToken;
        m_google_sheet_file_id = fileId;
        m_google_sheet_sheet_id = sheetId;
        _csvData = new List<Dictionary<string, string>>();
        _titles = new List<string>();
    }

    public GoogleSheetExtension Request<T>(Action<bool, string, List<T>> onCompleted) where T : IDataInitialized
    {
        return Request(delegate (bool result, string error, IList data)
        {
            try
            {
                List<T> arg = FromCSV2Object<T>();
                Log.Info($"Csv -> {typeof(T).Name} -> {result} {(result ? string.Empty : error)}");
                onCompleted?.Invoke(result, error, arg);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                onCompleted?.Invoke(arg1: false, ex.Message, null);
            }
        });
    }

    public GoogleSheetExtension Request(Action<bool, string, IList> onCompleted)
    {
        new HttpRequest().SetAuth(HttpRequest.Authorization.Bearer, m_google_api_token).Execute(URL, delegate (HttpResponse response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                onCompleted?.Invoke(arg1: false, response.Reason, null);
                return;
            }

            try
            {
                using (TextFieldParser parser = new TextFieldParser(new StringReader(response.Result)))
                {
                    parser.SetDelimiters(",");
                    parser.HasFieldsEnclosedInQuotes = true;

                    int rowCounter = 0;

                    while (!parser.EndOfData)
                    {
                        string[] fields = parser.ReadFields();
                        if (rowCounter < TitleRowCount)
                        {
                            if (rowCounter == 0)
                            {
                                _titles.AddRange(fields);
                            }

                            rowCounter++;
                            continue;
                        }

                        Dictionary<string, string> row = new Dictionary<string, string>();
                        for (int j = 0; j < _titles.Count; j++)
                        {
                            string value = j < fields.Length ? fields[j] : string.Empty;
                            value = value.Replace("\\n", "\n").Replace("\\`", ",").Replace("\\~", ";");
                            row[_titles[j]] = value;
                        }

                        _csvData.Add(row);
                    }
                }

                onCompleted?.Invoke(arg1: true, response.Result, _csvData);
            }
            catch (Exception ex)
            {
                onCompleted?.Invoke(arg1: false, ex.Message, null);
            }
        });
        return this;
    }

    public GoogleSheetExtension SetTitleRow(int rowCount)
    {
        TitleRowCount = rowCount;
        return this;
    }

    public bool ContainsColumn(string columnName)
    {
        return _titles.Contains(columnName);
    }

    public T GetValue<T>(string columnName, int rowIndex)
    {
        if (ContainsColumn(columnName))
        {
            return (T)Convert.ChangeType(_csvData[rowIndex][columnName], typeof(T));
        }

        return default(T);
    }

    public List<string> GetColumnData(string columnName)
    {
        List<string> list = new List<string>();
        foreach (Dictionary<string, string> csvDatum in _csvData)
        {
            if (csvDatum.ContainsKey(columnName))
            {
                list.Add(csvDatum[columnName]);
            }
        }

        return list;
    }

    public List<string> GetColumnData(int index)
    {
        List<string> list = new List<string>();
        foreach (Dictionary<string, string> csvDatum in _csvData)
        {
            list.Add(csvDatum.Values.ToList()[index]);
        }

        return list;
    }

    public Dictionary<string, string> GetRowData(int index)
    {
        if (_csvData.Count > index)
        {
            return _csvData[index];
        }

        return new Dictionary<string, string>();
    }

    public List<T> FromCSV2Object<T>() where T : IDataInitialized
    {
        List<T> list = new List<T>();
        for (int i = 0; i < RowCount; i++)
        {
            T val = (T)Activator.CreateInstance(typeof(T));
            PropertyInfo[] properties = val.GetType().GetProperties();
            PropertyInfo[] array = properties;
            foreach (PropertyInfo propertyInfo in array)
            {
                if (!propertyInfo.CanWrite || CustomAttribute.GetCustomAttribute<NonSerializedAttribute>(propertyInfo) != null || CustomAttribute.GetCustomAttribute<EnsignNonSerializedAttribute>(propertyInfo) != null)
                {
                    continue;
                }

                CSVBindAttribute customAttribute = CustomAttribute.GetCustomAttribute<CSVBindAttribute>(propertyInfo);
                string text = ((customAttribute != null) ? customAttribute.Name : propertyInfo.Name);
                try
                {
                    Dictionary<string, string> dictionary = _csvData[i];
                    object obj = (dictionary.ContainsKey(text) ? dictionary[text] : null);
                    if (obj != null)
                    {
                        if (propertyInfo.PropertyType.IsEnum)
                        {
                            if (obj is sbyte || obj is byte || obj is short || obj is ushort || obj is int || obj is uint || obj is long || obj is ulong)
                            {
                                propertyInfo.SetValue(val, Enum.ToObject(propertyInfo.PropertyType, obj), null);
                            }
                            else
                            {
                                propertyInfo.SetValue(val, Enum.Parse(propertyInfo.PropertyType, obj.ToString(), ignoreCase: true), null);
                            }
                        }
                        else if (string.IsNullOrWhiteSpace(obj.ToString()))
                        {
                            propertyInfo.SetValue(val, null, null);
                        }
                        else
                        {
                            propertyInfo.SetValue(val, Convert.ChangeType(obj, propertyInfo.PropertyType), null);
                        }
                    }
                    else
                    {
                        propertyInfo.SetValue(val, null, null);
                    }
                }
                catch
                {
                    throw new Exception($"Can't parse csv sheet '{m_google_sheet_sheet_id}' with column: '{text}' and row index: {i - TitleRowCount} with value: '{_csvData[i][text]}' to type: {propertyInfo.PropertyType.Name}");
                }
            }

            val.OnDataInitialized(this);
            list.Add(val);
        }

        return list;
    }
}