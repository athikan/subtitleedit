﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Logic.SubtitleFormats
{
    public class UnknownSubtitle35 : SubtitleFormat
    {

        //0072.08-0076.05
        //Pidin täna peaaegu surma saama,
        //kuna röövisid vale koera.

        //0076.09-0078.14
        //Mõtled seda tõsiselt või?

        private static readonly Regex RegexTimeCode = new Regex(@"^\d\d\d\d\.\d\d-\d\d\d\d\.\d\d$", RegexOptions.Compiled);

        public override string Extension
        {
            get { return ".txt"; }
        }

        public override string Name
        {
            get { return "Unknown 35"; }
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

        private static string MakeTimeCode(TimeCode tc)
        {
            return string.Format("{0:0000}.{1:00}", (int)tc.TotalSeconds, MillisecondsToFramesMaxFrameRate(tc.Milliseconds));
        }

        public override string ToText(Subtitle subtitle, string title)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < subtitle.Paragraphs.Count; i++)
            {
                Paragraph p = subtitle.Paragraphs[i];
                string text = Utilities.RemoveHtmlTags(p.Text);
                sb.AppendLine(string.Format("{0}-{1}\r\n{2}\r\n", MakeTimeCode(p.StartTime), MakeTimeCode(p.EndTime), text));
            }
            return sb.ToString();
        }

        private static TimeCode DecodeTimeCode(string timeCode)
        {
            string[] arr = timeCode.Split(":;,.".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            return new TimeCode(0, 0, int.Parse(arr[0]), FramesToMillisecondsMax999(int.Parse(arr[1])));
        }

        public override void LoadSubtitle(Subtitle subtitle, List<string> lines, string fileName)
        {
            _errorCount = 0;
            Paragraph p = null;
            var sb = new StringBuilder();
            foreach (string line in lines)
            {
                string s = line.Trim();
                if (RegexTimeCode.IsMatch(s))
                {
                    try
                    {
                        if (p != null)
                        {
                            p.Text = sb.ToString().Trim();
                            subtitle.Paragraphs.Add(p);
                        }
                        sb = new StringBuilder();
                        string[] arr = s.Split('-');
                        if (arr.Length == 2)
                            p = new Paragraph(DecodeTimeCode(arr[0]), DecodeTimeCode(arr[1]), string.Empty);
                    }
                    catch
                    {
                        _errorCount++;
                        p = null;
                    }
                }
                else if (s.Trim().Length > 0)
                {
                    sb.AppendLine(s);
                }
            }
            if (p != null)
            {
                p.Text = sb.ToString().Trim();
                subtitle.Paragraphs.Add(p);
            }
            subtitle.Renumber(1);
        }

    }
}
