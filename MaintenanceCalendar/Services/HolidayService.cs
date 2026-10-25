using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MaintenanceCalendar;

public static class HolidayService
{
    public const string Url = "https://www8.cao.go.jp/chosei/shukujitsu/syukujitsu.csv";
    /// <summary>
    /// 内閣府のCSVを取得し、Shift_JISで厳密に読み取って祝日データを返します。
    /// </summary>
    public static async Task<Dictionary<DateTime, string>> Fetch()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 2 * 1024 * 1024 };

        byte[] data = await client.GetByteArrayAsync(Url);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return Parse(Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(data));
    }

    /// <summary>
    /// CSVの見出し・日付・名称・重複を検証して、日付と祝日名の辞書へ変換します。
    /// </summary>
    public static Dictionary<DateTime, string> Parse(string csv)
    {
        var result = new Dictionary<DateTime, string>(); using var reader = new StringReader(csv);

        var header = reader.ReadLine();

        if (header is null || !header.Contains("国民の祝日")) throw new InvalidDataException("内閣府CSVの見出しを確認できません。");

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cells = line.Split(',');

            if (cells.Length != 2 || !DateTime.TryParseExact(cells[0].Trim('"'), "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || string.IsNullOrWhiteSpace(cells[1]))
                throw new InvalidDataException("祝日CSVに不正な行があります。");

            if (!result.TryAdd(date, cells[1].Trim('"'))) throw new InvalidDataException("祝日の日付が重複しています。");

        }

        if (result.Count == 0) throw new InvalidDataException("祝日データが空です。"); return result;
    }
}

