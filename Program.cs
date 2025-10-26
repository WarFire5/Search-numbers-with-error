using System.Text;

class Program
{
    const string LawsFile = "номера_законов.txt";
    const string CourtsFile = "номера_судебных_решений.txt";

    static string[] AllReal = [];
    static Dictionary<(string letters, string digits), List<string>> Index = default!;

    static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);

        AllReal = [.. ReadAllLinesSmart(LawsFile)
                 .Concat(ReadAllLinesSmart(CourtsFile))
                 .Select(s => s.Trim()).Where(s => s.Length > 0)];
        Index = BuildIndex(AllReal);

        Console.WriteLine("\nВведите номер закона либо судебного решения:" +
            "\n(нажатие клавиш space и enter без предварительного ввода номера завершает программу)\n");
        while (true)
        {
            var q = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(q)) break;

            var best = FindBest(q!);
            if (best.Count == 0)
            {
                Console.WriteLine("К сожалению, похожих номеров не найдено μ_μ.\n");
                continue;
            }

            bool isExact = Fold(best[0]) == Fold(q);
            if (isExact)
            {
                Console.Write(best[0]);
                Console.WriteLine(" - совпадение 100% :).\n");
            }
            else
            {
                Console.WriteLine($"Наиболее близкий номер: {best[0]}.\n");
                if (best.Count > 1)
                    Console.WriteLine($"Посмотрите также: [{string.Join(" | ", best.Skip(1).Take(2))}].\n");
            }
        }
    }

    static List<string> FindBest(string query)
    {
        var qf = Fold(query);
        var qKey = (LettersOnly(qf), TrimLeadingZeros(DigitsOnly(qf)));
        var qCore = LettersDigitsCore(qf);

        if (qCore.Length == 0) return [];

        if (Index.TryGetValue(qKey, out var bucket) && bucket.Count > 0)
            return RankAndFilter(bucket, qCore);

        return RankAndFilter(AllReal, qCore);

        List<string> RankAndFilter(IEnumerable<string> source, string qCoreLocal)
        {
            var ranked = source
                .Select(c =>
                {
                    var cf = Fold(c);
                    var cc = LettersDigitsCore(cf);
                    return new { c, d = Levenshtein(qCoreLocal, cc), coreLen = Math.Max(qCoreLocal.Length, cc.Length) };
                })
                .OrderBy(x => x.d).ThenBy(x => x.c, StringComparer.Ordinal)
                .Take(3)
                .ToList();

            if (ranked.Count == 0) return [];

            var best = ranked[0];
            double sim = best.coreLen == 0 ? 0.0 : 1.0 - (double)best.d / best.coreLen;
            if (sim < 0.5) return [];

            return ranked.Select(x => x.c).ToList();
        }
    }

    static Dictionary<(string letters, string digits), List<string>> BuildIndex(IEnumerable<string> items)
    {
        var map = new Dictionary<(string, string), List<string>>(StringTupleComparer.Ordinal);
        foreach (var orig in items)
        {
            var f = Fold(orig);
            var key = (LettersOnly(f), TrimLeadingZeros(DigitsOnly(f)));
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<string>(1);
            list.Add(orig);
        }
        return map;
    }

    static readonly Dictionary<char, char> LatinToCyr = new()
    {
        ['A'] = 'А',
        ['B'] = 'В',
        ['C'] = 'С',
        ['E'] = 'Е',
        ['H'] = 'Н',
        ['K'] = 'К',
        ['M'] = 'М',
        ['O'] = 'О',
        ['P'] = 'Р',
        ['T'] = 'Т',
        ['X'] = 'Х',
        ['Y'] = 'У',
        ['a'] = 'А',
        ['b'] = 'В',
        ['c'] = 'С',
        ['e'] = 'Е',
        ['h'] = 'Н',
        ['k'] = 'К',
        ['m'] = 'М',
        ['o'] = 'О',
        ['p'] = 'Р',
        ['t'] = 'Т',
        ['x'] = 'Х',
        ['y'] = 'У',
    };
    static readonly Dictionary<char, char> CyrEq = new() { ['Ё'] = 'Е', ['ё'] = 'Е', ['Й'] = 'И', ['й'] = 'И' };
    static readonly Dictionary<char, char> Sep = new()
    {
        ['-'] = '-',
        ['—'] = '-',
        ['–'] = '-',
        ['_'] = '-',
        ['/'] = '/',
        ['\\'] = '/',
        ['.'] = '.',
        [','] = ',',
        [':'] = ':',
        ['('] = '(',
        [')'] = ')',
        ['['] = '[',
        [']'] = ']',
        ['{'] = '{',
        ['}'] = '}',
        ['@'] = '@',
        ['»'] = '\0',
        ['«'] = '\0',
        ['"'] = '\0',
        ['\''] = '\0',
    };

    static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var raw in s.Normalize(NormalizationForm.FormKC))
        {
            var ch = raw;
            if (char.IsWhiteSpace(ch)) continue;
            if (CyrEq.TryGetValue(ch, out var ce)) ch = ce;
            if (LatinToCyr.TryGetValue(ch, out var lc)) ch = lc;
            if (Sep.TryGetValue(ch, out var rep)) { if (rep == '\0') continue; ch = rep; }
            if (char.IsDigit(ch) || IsCyr(ch) || "-/().,@[]{}".IndexOf(ch) >= 0)
                sb.Append(char.ToUpperInvariant(ch));
        }
        return sb.ToString();

        static bool IsCyr(char c) => (c >= 'А' && c <= 'Я') || (c >= 'а' && c <= 'я');
    }

    static string LettersOnly(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) if (ch >= 'А' && ch <= 'Я') sb.Append(ch);
        return sb.ToString();
    }
    static string DigitsOnly(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) if (char.IsDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }
    static string LettersDigitsCore(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) if (char.IsDigit(ch) || (ch >= 'А' && ch <= 'Я')) sb.Append(ch);
        return sb.ToString();
    }
    static string TrimLeadingZeros(string s)
    {
        int i = 0; while (i < s.Length && s[i] == '0') i++;
        return i == s.Length ? "0" : s[i..];
    }

    static int Levenshtein(string a, string b)
    {
        int n = a.Length, m = b.Length;
        if (n == 0) return m; if (m == 0) return n;
        var prev = new int[m + 1];
        var cur = new int[m + 1];
        for (int j = 0; j <= m; j++) prev[j] = j;
        for (int i = 1; i <= n; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
            }
            Array.Copy(cur, prev, m + 1);
        }
        return prev[m];
    }

    static string[] ReadAllLinesSmart(string path)
    {
        var utf8 = new UTF8Encoding(false, false);
        var lines = ReadAllLines(path, utf8);
        if (!HasReplacementChar(lines)) return lines;

        var win1251 = Encoding.GetEncoding(1251);
        return ReadAllLines(path, win1251);

        static string[] ReadAllLines(string p, Encoding enc)
        {
            using var sr = new StreamReader(p, enc, detectEncodingFromByteOrderMarks: true);
            var list = new List<string>();
            while (!sr.EndOfStream) list.Add(sr.ReadLine() ?? "");
            return [.. list];
        }
        static bool HasReplacementChar(IEnumerable<string> ls)
            => ls.Any(s => s.Contains('\uFFFD'));
    }

    sealed class StringTupleComparer : IEqualityComparer<(string, string)>
    {
        public static readonly StringTupleComparer Ordinal = new();
        public bool Equals((string, string) x, (string, string) y) =>
            string.Equals(x.Item1, y.Item1, StringComparison.Ordinal) &&
            string.Equals(x.Item2, y.Item2, StringComparison.Ordinal);
        public int GetHashCode((string, string) t) => HashCode.Combine(t.Item1, t.Item2);
    }
}