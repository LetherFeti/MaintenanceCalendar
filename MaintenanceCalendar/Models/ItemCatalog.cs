using Microsoft.VisualBasic.FileIO;
using System.Text;

namespace MaintenanceCalendar;

/// <summary>アプリに同梱した2列CSVからメーカーとモデルの入力候補を読み込みます。</summary>
internal sealed class ItemCatalog
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Catalog", "manufacturer-model.csv");
    public List<(string Manufacturer, string Model)> Entries { get; } = [];
    public string? Error { get; private set; }

    /// <summary>引用符・カンマ・BOMに対応して読み込み、重複と空行を除きます。</summary>
    public ItemCatalog(string path)
    {
        try
        {
            using var parser = new TextFieldParser(path, new UTF8Encoding(false, true), true) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
            parser.SetDelimiters(",");
            var header = parser.ReadFields();
            if (header is not { Length: 2 } || !new[] { "メーカー", "Manufacturer" }.Contains(header[0]) || !new[] { "モデル", "Model" }.Contains(header[1]))
                throw new InvalidDataException("CSVの先頭行は「メーカー,モデル」にしてください。");
            while (!parser.EndOfData)
            {
                var row = parser.ReadFields();
                if (row is not { Length: 2 }) throw new InvalidDataException("CSVはメーカー・モデルの2列で指定してください。");
                string maker = row[0].Trim(), model = row[1].Trim();
                if (maker.Length == 0 && model.Length == 0) continue;
                if (maker.Length is 0 or > 100 || model.Length is 0 or > 150) throw new InvalidDataException("メーカーは1～100文字、モデルは1～150文字で指定してください。");
                if (!Entries.Any(x => x.Manufacturer.Equals(maker, StringComparison.OrdinalIgnoreCase) && x.Model.Equals(model, StringComparison.OrdinalIgnoreCase))) Entries.Add((maker, model));
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or MalformedLineException or DecoderFallbackException)
        {
            Entries.Clear(); Error = "候補CSVを読み込めませんでした。手入力は利用できます。";
            LogManager.GetCurrentClassLogger().Warn(ex, "Cannot load item catalog {0}", path);
        }
    }

    /// <summary>入力文字を含むメーカーを、前方一致を優先して返します。</summary>
    public IEnumerable<InputSuggestion> Manufacturers(string query) => Entries.Select(x => x.Manufacturer).Distinct(StringComparer.OrdinalIgnoreCase)
        .Where(x => x.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(x => x.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase)).ThenBy(x => x)
        .Select(x => new InputSuggestion(x, x));

    /// <summary>指定メーカーに対応するモデル候補を返し、メーカー未入力時は選択と同時に補います。</summary>
    public IEnumerable<InputSuggestion> Models(string manufacturer, string query) => Entries
        .Where(x => (string.IsNullOrWhiteSpace(manufacturer) || x.Manufacturer.Equals(manufacturer.Trim(), StringComparison.OrdinalIgnoreCase)) && x.Model.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(x => x.Model.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase)).ThenBy(x => x.Model)
        .Select(x => new InputSuggestion(x.Model, string.IsNullOrWhiteSpace(manufacturer) ? $"{x.Model}  /  {x.Manufacturer}" : x.Model, x.Manufacturer));
}

/// <summary>入力欄に確定する文字と候補一覧での表示文字です。</summary>
internal sealed record InputSuggestion(string Value, string Label, string? Manufacturer = null);
