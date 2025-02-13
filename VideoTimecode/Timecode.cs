﻿using System;
using System.Text.RegularExpressions;

namespace VideoTimecode
{
    public class Timecode
    {
        public const string FramesSeparatorDot = ".";
        public const string FramesSeparatorSemiColon = ".";
        public const string FramesSeparatorColon = ".";

        private const int SecondsInHour = 3600;
        private const int SecondsInMinute = 60;
        private const int MinutesInHour = 60;
        private const int MillisecondsInSecond = 1000;
        private const int HoursInDay = 24;
        private const int EveryTenMinute = 10;

        private const string TimeCodePattern = @"^(?<hours>[0-2][0-9]):(?<minutes>[0-5][0-9]):(?<seconds>[0-5][0-9])[:|;|\.](?<frames>[0-9]{2,3})$";

        public FrameRate FrameRate { get; }
        private bool IsDropFrameRate => FrameRate.DropFramesCount != 0;
        public int TotalFrames { get; private set; }
        public int Hours { get; private set; }
        public int Minutes { get; private set; }
        public int Seconds { get; private set; }
        public int Frames { get; private set; }

        public Timecode(int totalFrames, FrameRate frameRate)
        {
            FrameRate = frameRate;

            TotalFrames = totalFrames;

            var timespan = TimeSpan.FromMilliseconds(TotalFrames * MillisecondsInSecond / FrameRate.Rate);
            Hours = timespan.Hours;
            Minutes = timespan.Minutes;
            Seconds = timespan.Seconds;

            UpdateByTotalFrames();
        }

        public Timecode(string timecode, FrameRate frameRate)
        {
            FrameRate = frameRate;

            if (string.IsNullOrEmpty(timecode))
            {
                throw new ArgumentNullException(nameof(timecode));
            }

            var tcRegex = new Regex(TimeCodePattern);
            var match = tcRegex.Match(timecode);
            if (!match.Success)
            {
                throw new ArgumentException("Input text was not in valid timecode format.", nameof(timecode));
            }

            Hours = int.Parse(match.Groups["hours"].Value);
            Minutes = int.Parse(match.Groups["minutes"].Value);
            Seconds = int.Parse(match.Groups["seconds"].Value);
            Frames = int.Parse(match.Groups["frames"].Value);

            CalculateTotalFrames();
        }

        public Timecode(TimeSpan timespan, FrameRate frameRate, bool ceiling = true)
        {
            FrameRate = frameRate;

            if (ceiling)
            {
                TotalFrames = (int)Math.Ceiling(timespan.TotalMilliseconds * frameRate.Rate) / MillisecondsInSecond;
            }
            else
            {
                TotalFrames = (int)Math.Floor(timespan.TotalMilliseconds * frameRate.Rate) / MillisecondsInSecond;
            }

            UpdateByTotalFrames();
        }

        public override string ToString()
        {
            var frameSeparator = IsDropFrameRate ? ";" : ":";
            return $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}{frameSeparator}{Frames:D2}";
        }

        public string ToString(string framesSeparator)
        {
            return $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}{framesSeparator}{Frames:D2}";
        }

        public TimeSpan ToTimeSpan()
        {
            var framesInMilliseconds = TotalFrames * MillisecondsInSecond / FrameRate.Rate;
            return TimeSpan.FromMilliseconds(framesInMilliseconds);
        }

        private void CalculateTotalFrames()
        {
            double frames = Hours * SecondsInHour;
            frames += Minutes * SecondsInMinute;
            frames += Seconds;
            frames *= FrameRate.RateRounded;
            frames += Frames;

            if (IsDropFrameRate)
            {
                var totalMinutes = Hours * MinutesInHour + Minutes;
                totalMinutes -= totalMinutes / EveryTenMinute;
                var dropFrames = totalMinutes * FrameRate.DropFramesCount;
                frames -= dropFrames;
            }

            TotalFrames = (int)Math.Floor(frames);
        }

        private void UpdateByTotalFrames()
        {
            var frameCount = TotalFrames;
            if (IsDropFrameRate)
            {
                var fps = FrameRate.Rate;
                var dropFramesCount = FrameRate.DropFramesCount;
                var framesPerHour = Math.Round(fps * SecondsInHour, MidpointRounding.AwayFromZero);
                var framesPer24H = framesPerHour * HoursInDay;
                var framesPer10M = Math.Round(fps * SecondsInMinute * EveryTenMinute, MidpointRounding.AwayFromZero);
                var framesPerMin = Math.Round(fps * SecondsInMinute, MidpointRounding.AwayFromZero);

                frameCount %= (int)framesPer24H;

                var tenMinutesIntervalsCount = Math.Floor(frameCount / framesPer10M);
                var smallestTenMinutesIntervalLength = frameCount % framesPer10M;
                if (smallestTenMinutesIntervalLength > dropFramesCount)
                {
                    frameCount += (int)(dropFramesCount * (EveryTenMinute - 1) * tenMinutesIntervalsCount + dropFramesCount * Math.Floor((smallestTenMinutesIntervalLength - dropFramesCount) / framesPerMin));
                }
                else
                {
                    frameCount += (int)(dropFramesCount * (EveryTenMinute - 1) * tenMinutesIntervalsCount);
                }

                Hours = (int)Math.Floor(Math.Floor(Math.Floor(frameCount / (double)FrameRate.RateRounded) / SecondsInMinute) / SecondsInMinute);
                Minutes = (int)Math.Floor(Math.Floor(frameCount / (double)FrameRate.RateRounded) / SecondsInMinute) % SecondsInMinute;
                Seconds = (int)Math.Floor(frameCount / (double)FrameRate.RateRounded) % SecondsInMinute;
                Frames = frameCount % FrameRate.RateRounded;
            }
            else
            {
                Hours = frameCount / (SecondsInHour * FrameRate.RateRounded) % HoursInDay;
                if (Hours >= HoursInDay)
                {
                    Hours %= HoursInDay;
                    frameCount -= (HoursInDay - 1) * SecondsInHour * FrameRate.RateRounded;
                }
                Minutes = frameCount % (SecondsInHour * FrameRate.RateRounded) / (SecondsInMinute * FrameRate.RateRounded);
                Seconds = frameCount % (SecondsInHour * FrameRate.RateRounded) % (SecondsInMinute * FrameRate.RateRounded) / FrameRate.RateRounded;
                Frames = frameCount % (SecondsInHour * FrameRate.RateRounded) % (SecondsInMinute * FrameRate.RateRounded) % FrameRate.RateRounded;
            }
        }
    }
}
