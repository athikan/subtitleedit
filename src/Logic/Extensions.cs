﻿using System;
using System.Text;

namespace Nikse.SubtitleEdit.Logic
{
    public static class Extensions
    {

        public static bool StartsWith(this String s, char c)
        {
            return s.Length > 0 && s[0] == c;
        }

        public static bool StartsWith(this StringBuilder sb, char c)
        {
            return sb.Length > 0 && sb[0] == c;
        }

        public static bool EndsWith(this String s, char c)
        {
            return s.Length > 0 && s[s.Length - 1] == c;
        }

        public static bool EndsWith(this StringBuilder sb, char c)
        {
            return sb.Length > 0 && sb[sb.Length - 1] == c;
        }

        public static bool Contains(this string s, char c)
        {
            return s.IndexOf(c) != -1;
        }

    }
}
