﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Logic.SubtitleFormats
{
    public class UnknownSubtitle20 : SubtitleFormat
    {
        private static Regex _regexTimeCode1 = new Regex(@"^     \d\d:\d\d:\d\d:\d\d     \d\d\d\d            ", RegexOptions.Compiled);
        private static Regex _regexTimeCode1Empty = new Regex(@"^     \d\d:\d\d:\d\d:\d\d     \d\d\d\d$", RegexOptions.Compiled);
        private static Regex _regexTimeCode2 = new Regex(@"^     \d\d:\d\d:\d\d:\d\d     \d\d\d\-\d\d          ", RegexOptions.Compiled);

        public override string Extension
        {
            get { return ".C"; }
        }

        public override string Name
        {
            get { return "Unknown 20"; }
        }

        public override bool IsTimeBased
        {
            get { return true; }
        }

        public override bool IsMine(List<string> lines, string fileName)
        {
            var subtitle = new Subtitle();
            LoadSubtitle(subtitle, lines, fileName);
            return subtitle.Paragraphs.Count > _errorCount;
        }

        public override string ToText(Subtitle subtitle, string title)
        {
            var sb = new StringBuilder();
            int number = 1;
            int number2 = 0;
            foreach (Paragraph p in subtitle.Paragraphs)
            {
                string line1 = string.Empty;
                string line2 = string.Empty;
                string[] lines = p.Text.Replace(Environment.NewLine, "\r").Split('\r');
                if (lines.Length > 2)
                    lines = Utilities.AutoBreakLine(p.Text).Replace(Environment.NewLine, "\r").Split('\r');
                if (lines.Length == 1)
                {
                    line2 = lines[0];
                }
                else
                {
                    line1 = lines[0];
                    line2 = lines[1];
                }

                // line 1
                sb.Append(string.Empty.PadLeft(5, ' '));
                sb.Append(p.StartTime.ToHHMMSSFF());
                sb.Append(string.Empty.PadLeft(5, ' '));
                sb.Append(number.ToString().PadLeft(4, '0'));
                sb.Append(string.Empty.PadLeft(12, ' '));
                sb.AppendLine(line1);

                //line 2
                sb.Append(string.Empty.PadLeft(5, ' '));
                sb.Append(p.EndTime.ToHHMMSSFF());
                sb.Append(string.Empty.PadLeft(5, ' '));
                sb.Append((number2 / 7 + 1).ToString().PadLeft(3, '0'));
                sb.Append('-');
                sb.Append((number2 % 7 + 1).ToString().PadLeft(2, '0'));
                sb.Append(string.Empty.PadLeft(10, ' '));
                sb.AppendLine(line2);
                sb.AppendLine();

                number++;
                number2++;
            }
            return sb.ToString();
        }

        private static TimeCode DecodeTimeCode(string timeCode)
        {
            string[] arr = timeCode.Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            return new TimeCode(int.Parse(arr[0]), int.Parse(arr[1]), int.Parse(arr[2]), FramesToMillisecondsMax999(int.Parse(arr[3])));
        }

        public override void LoadSubtitle(Subtitle subtitle, List<string> lines, string fileName)
        {
            _errorCount = 0;
            Paragraph p = null;
            foreach (string line in lines)
            {
                string s = line.TrimEnd();
                if (_regexTimeCode1.IsMatch(s))
                {
                    try
                    {
                        if (p != null)
                            subtitle.Paragraphs.Add(p);
                        p = new Paragraph(DecodeTimeCode(s.Substring(5, 11)), new TimeCode(0, 0, 0, 0), s.Remove(0, 37).Trim());
                    }
                    catch
                    {
                        _errorCount++;
                        p = null;
                    }
                }
                else if (_regexTimeCode1Empty.IsMatch(s))
                {
                    try
                    {
                        if (p != null)
                            subtitle.Paragraphs.Add(p);
                        p = new Paragraph(DecodeTimeCode(s.Substring(5, 11)), new TimeCode(0, 0, 0, 0), string.Empty);
                    }
                    catch
                    {
                        _errorCount++;
                        p = null;
                    }
                }
                else if (_regexTimeCode2.IsMatch(s))
                {
                    try
                    {
                        if (p != null)
                        {
                            p.EndTime = DecodeTimeCode(s.Substring(5, 11));
                            if (p.Text.Trim().Length == 0)
                                p.Text = s.Remove(0, 37).Trim();
                            else
                                p.Text = p.Text + Environment.NewLine + s.Remove(0, 37).Trim();
                        }
                    }
                    catch
                    {
                        _errorCount++;
                        p = null;
                    }
                }
                else if (s.Trim().Length > 0)
                {
                    _errorCount++;
                }
            }
            if (p != null)
                subtitle.Paragraphs.Add(p);
            subtitle.Renumber(1);
        }

    }
}
