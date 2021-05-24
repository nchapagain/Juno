namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.CRC.Extensions;

    internal static class CronTabValidation
    {
        private const string Rules = @"^(?<min>(\d+|\*|\d+-\d+|\d+(,\d+)+|\d+/\d+|\*/\d+))\s(?<hour>(\d+|\*|\d+-\d+|\d+(,\d+)+|\d+/\d+|\*/\d+))\s(?<day>(\d+|\?|\*(/\d+)*|L|L-\d+|\dL|\d+/\d+|LW|\d+W|\d+(,\d+)+))\s(?<month>(\d+|\*|\w+(,\w+)*|\d+-\d+))\s(?<dayweek>(\d+|\w+|\*|\?|\w+-\w+|\dL|\d+#\d+|\w+(,\w+)))$";
        private static readonly char[] SlipNumbers = new char[] { '*', '-', '/', ',', 'L', 'W', '?' };
        private static readonly char[] SlipNumbersMonth = new char[] { '*', '-', '/', ',' };
        private static readonly char[] SlipNumbersDayWeek = new char[] { '*', '-', '/', ',', '#', '?', 'L' };
        private static readonly Regex CrontabRegex = new Regex(CronTabValidation.Rules, RegexOptions.Compiled);

        public static bool Validate(string crontab, out string errorMessage)
        {
            crontab.ThrowIfNullOrWhiteSpace(nameof(crontab));

            errorMessage = null;

            var match = CronTabValidation.CrontabRegex.Match(crontab);
            if (!match.Success)
            {
                errorMessage = $"Invalid crontab expression: {crontab}";
                return false;
            }
            else
            {
                // Minutes
                var min = match.Groups["min"];
                if (!CronTabValidation.ValidateIntegers(min.Value, 0, 59, out errorMessage))
                {
                    return false;
                }

                // Hour
                var hour = match.Groups["hour"];
                if (!CronTabValidation.ValidateIntegers(hour.Value, 0, 24, out errorMessage)) 
                {
                    return false;
                }

                // Day
                var day = match.Groups["day"];
                if (!CronTabValidation.ValidateIntegers(day.Value, 1, 31, out errorMessage))
                {
                    return false;
                }

                // Month
                var month = match.Groups["month"];
                if (!CronTabValidation.ValidateMonth(month.Value, out errorMessage))
                {
                    return false;
                }

                // DayWeek
                var dayweek = match.Groups["dayweek"];
                if (!CronTabValidation.ValidateDayWeek(dayweek.Value, out errorMessage))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateIntegers(string value, int min, int max, out string errorMessage)
        {
            var numbers = value.Split(CronTabValidation.SlipNumbers, StringSplitOptions.RemoveEmptyEntries);
            errorMessage = null;

            foreach (var num in numbers)
            {
                if (int.TryParse(num, out var numAsInteger))
                {
                    if (!(numAsInteger >= min && numAsInteger <= max))
                    {
                        errorMessage = $"Invalid value {value}";
                        return false;
                    }
                }
                else
                {
                    errorMessage = $"Invalid value {value}";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateMonth(string value, out string errorMessage)
        {
            var values = value.Split(CronTabValidation.SlipNumbersMonth, StringSplitOptions.RemoveEmptyEntries);
            errorMessage = null;

            foreach (var val in values)
            {
                if (int.TryParse(val, out var numAsInteger))
                {
                    if (!(numAsInteger >= 0 && numAsInteger <= 12))
                    {
                        errorMessage = $"Invalid value {value}";
                        return false;
                    }
                }
                else if (!Enum.TryParse<Month>(val, false, out var month))
                {
                    errorMessage = $"Invalid value {value}";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateDayWeek(string value, out string errorMessage)
        {
            var values = value.Split(CronTabValidation.SlipNumbersDayWeek, StringSplitOptions.RemoveEmptyEntries);
            errorMessage = null;
            foreach (var val in values)
            {
                if (int.TryParse(val, out var numAsInteger))
                {
                    if (!(numAsInteger >= 0 && numAsInteger <= 7))
                    {
                        errorMessage = $"Invalid value {value}";
                        return false;
                    }
                }
                else if (!Enum.TryParse<WeekDay>(val, true, out var day))
                {
                    errorMessage = $"Invalid value {value}";
                    return false;
                }
            }

            return true;
        }
    }
}
