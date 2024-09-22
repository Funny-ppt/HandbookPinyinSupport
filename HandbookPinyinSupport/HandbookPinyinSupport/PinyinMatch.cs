using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using TinyPinyin;

namespace CookbookPinyinSupport;

internal class PinyinMatch
{
    public static PinyinMatch Instance { get; private set; } = new();

    private ConcurrentDictionary<string, (string firstLetters, string full)> cache = new();
    private Dictionary<char, string> specialCases = new Dictionary<char, string>()
    {
        { '镐', "GAO" },
        { '腌', "YAN" },
    };

    /// <summary>
    /// 假定文本仅包含汉字且长度不超过16，获取文本的拼音与首字母拼音。有待优化性能/多音字支持
    /// </summary>
    public (string firstLetters, string full) GetPinyin(string text)
    {
        Debug.Assert(text.Length > 0 && text.Length <= 16);
        Span<char> buf0 = stackalloc char[16];
        Span<char> buf1 = stackalloc char[96];
        int pbuf1 = 0;
        var pinyin = specialCases.GetValueOrDefault(text[0]) ?? PinyinHelper.GetPinyin(text[0]);
        buf0[0] = pinyin[0];
        pinyin.CopyTo(buf1);
        pbuf1 += pinyin.Length;
        for (int i = 1; i < text.Length; ++i)
        {
            pinyin = specialCases.GetValueOrDefault(text[i]) ?? PinyinHelper.GetPinyin(text[i]);
            //buf1[pbuf1++] = ' '; // uncomment this if sperator character is needed
            buf0[i] = pinyin[0];
            pinyin.CopyTo(buf1[pbuf1..]);
            pbuf1 += pinyin.Length;
        }
        return (new string(buf0[..text.Length]), new string(buf1[..pbuf1]));
    }
    public enum MatchType
    {
        NoneMatch = 0,
        FullMatch,
        StartsWith,
        Contains,
        FuzzyMatch,
    }
    public struct MatchResult
    {
        public MatchType firstLettersMatchType;
        public float firstLettersScore;
        public MatchType fullMatchType;
        public float fullScore;

        public readonly bool HasResult => firstLettersMatchType != MatchType.NoneMatch || fullMatchType != MatchType.NoneMatch;
    }
    /// <summary>
    /// 模糊匹配字符串, 返回值越大相似度越大, 并且考虑了全拼有更高匹配优先级以及字串有更高优先级
    /// 但是对于缩写要求相似度至少大于Threshold0，对于全拼要求相似度至少大于Threshold1
    /// </summary>
    public MatchResult FuzzyMatch(string text, string pattern)
    {
        MatchResult res = default;
        pattern = pattern?.Trim().ToUpper();
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern) || text.Length > 16 || pattern.Length > 63) return res;

        if (cache.TryGetValue(text, out var pinyin) || cache.TryAdd(text, pinyin = GetPinyin(text)))
        {
            if (pinyin.firstLetters.Length + 1 >= pattern.Length)
            {
                if (pinyin.firstLetters.Equals(pattern))
                {
                    res.firstLettersMatchType = MatchType.FullMatch;
                    res.firstLettersScore = 3f;
                }
                else if (pinyin.firstLetters.StartsWith(pattern))
                {
                    res.firstLettersMatchType = MatchType.StartsWith;
                    res.firstLettersScore = 2.5f;
                }
                else if (pinyin.firstLetters.Contains(pattern))
                {
                    res.firstLettersMatchType = MatchType.Contains;
                    res.firstLettersScore = 2f;
                }
                else
                {
                    var maxLen = Math.Max(pinyin.firstLetters.Length, pattern.Length);
                    var similarity = 1f - (float)LevenshteinDistance(pinyin.firstLetters, pattern) / maxLen;
                    if (similarity >= 0.5f)
                    {
                        res.firstLettersMatchType = MatchType.FuzzyMatch;
                        res.firstLettersScore = 1f + similarity;
                    }
                }
            }
            if (pinyin.full.Length + 2 >= pattern.Length)
            {
                if (pinyin.full.Equals(pattern))
                {
                    res.fullMatchType = MatchType.FullMatch;
                    res.fullScore = 3f;
                }
                else if (pinyin.full.StartsWith(pattern))
                {
                    res.fullMatchType = MatchType.StartsWith;
                    res.fullScore = 2.5f;
                }
                else if (pinyin.full.Contains(pattern))
                {
                    res.fullMatchType = MatchType.Contains;
                    res.fullScore = 2f;
                }
                else
                {
                    var maxLen = Math.Max(pinyin.full.Length, pattern.Length);
                    var similarity = 1f - (float)LevenshteinDistance(pinyin.full, pattern) / maxLen;
                    if (similarity >= 0.3f)
                    {
                        res.fullMatchType = MatchType.FuzzyMatch;
                        res.fullScore = 1f + similarity;
                    }
                }
            }
        }
        return res;
    }

    /// <summary>
    /// 计算字符串的LevenshteinDistance。输入必须非null且长度不为 0, 长度小于64
    /// </summary>
    public static int LevenshteinDistance(string a, string b)
    {
        Debug.Assert(!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && a.Length < 64 && b.Length < 64);
        if (a == b) return 0;
        Span<int> prevRow = stackalloc int[64];
        Span<int> currentRow = stackalloc int[64];

        for (int j = 0; j <= b.Length; j++)
        {
            prevRow[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            currentRow[0] = i;

            for (int j = 1; j <= b.Length; j++)
            {
                int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                currentRow[j] = Math.Min(Math.Min(currentRow[j - 1] + 1, prevRow[j] + 1), prevRow[j - 1] + cost);
            }

            Span<int> temp = prevRow;
            prevRow = currentRow;
            currentRow = temp;
        }

        return prevRow[b.Length];
    }

}
