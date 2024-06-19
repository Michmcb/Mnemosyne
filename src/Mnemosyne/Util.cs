namespace Mnemosyne
{
	using MichMcb.CsExt.Dates;
	using System;
	using System.Threading.Tasks;

	public static class Util
	{
		public static async Task<bool> Try(int count, string tryMsg, string failMsg, Action action)
		{
			for (int i = 1; i <= count; i++)
			{
				try
				{
					await Console.Out.WriteLineAsync(tryMsg);
					action();
					return true;
				}
				catch (Exception ex)
				{
					await Console.Out.WriteLineAsync(failMsg + ex.Message);
					await Task.Delay(Random.Shared.Next(1000, 5000));
				}
			}
			return false;
		}
		public static async Task<bool> Try(int count, string tryMsg, string failMsg, Func<Task> action)
		{
			for (int i = 1; i <= count; i++)
			{
				try
				{
					await Console.Out.WriteLineAsync(tryMsg);
					await action();
					return true;
				}
				catch (Exception ex)
				{
					await Console.Out.WriteLineAsync(failMsg + ex.Message);
					await Task.Delay(Random.Shared.Next(1000, 5000));
				}
			}
			return false;
		}
		public static string DateTimeOffsetToString(DateTimeOffset d)
		{
			// yyyy-MM-dd HH-mm-ss+HH-mm
			// 0123456789012345678901234
			return string.Create(Iso8601Format.ExtendedFormat_NoFractional_FullTz.LengthRequired, d, (str, dto) =>
			{
				UtcDateTime.FromDateTimeOffset(dto).Format(str, null, Iso8601Format.ExtendedFormat_NoFractional_FullTz);

				// Replace the colons with dashes
				str[22] = '-';
				str[16] = '-';
				str[13] = '-';
			});
		}
		public static bool TryParseDateTimeOffset(ReadOnlySpan<char> str, out DateTimeOffset d)
		{
			// yyyy-MM-dd HH-mm-ss+HH-mm
			// 0123456789012345678901234
			if (str.Length < 25)
			{
				d = default;
				return false;
			}
			if (int.TryParse(str.Slice(0, 4), out int year)
				&& int.TryParse(str.Slice(5, 2), out int month)
				&& int.TryParse(str.Slice(8, 2), out int day)
				&& int.TryParse(str.Slice(11, 2), out int hour)
				&& int.TryParse(str.Slice(14, 2), out int minute)
				&& int.TryParse(str.Slice(17, 2), out int second)
				&& int.TryParse(str.Slice(20, 2), out int tzHour)
				&& int.TryParse(str.Slice(23, 2), out int tzMinute))
			{
				char pm = str[19];
				int mult;
				switch (pm)
				{
					case '+':
						mult = 1;
						break;
					case '-':
						mult = -1;
						break;
					default:
						d = default;
						return false;
				}
				TimeSpan offset = TimeSpan.FromMinutes((tzHour * 60 + tzMinute) * mult);
				d = new DateTimeOffset(year, month, day, hour, minute, second, offset);
				return true;
			}
			d = default;
			return false;
		}
	}
}
