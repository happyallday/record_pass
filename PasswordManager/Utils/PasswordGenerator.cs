using System;
using System.Linq;
using System.Text;

namespace PasswordManager.Utils
{
    public class PasswordGenerator
    {
        private const string LowercaseChars = "abcdefghjkmnpqrstuvwxyz";
        private const string UppercaseChars = "ABCDEFGHJKMNPQRSTUVWXYZ";
        private const string DigitChars = "23456789";
        private const string SpecialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        public class PasswordGeneratorOptions
        {
            public int Length { get; set; } = 16;
            public bool IncludeUppercase { get; set; } = true;
            public bool IncludeLowercase { get; set; } = true;
            public bool IncludeDigits { get; set; } = true;
            public bool IncludeSpecialChars { get; set; } = true;
        }

        public static string GeneratePassword(PasswordGeneratorOptions options)
        {
            if (options.Length < 8)
                options.Length = 8;
            if (options.Length > 128)
                options.Length = 128;

            var chars = new StringBuilder();
            
            if (options.IncludeLowercase)
                chars.Append(LowercaseChars);
            if (options.IncludeUppercase)
                chars.Append(UppercaseChars);
            if (options.IncludeDigits)
                chars.Append(DigitChars);
            if (options.IncludeSpecialChars)
                chars.Append(SpecialChars);

            if (chars.Length == 0)
                chars.Append(LowercaseChars);

            var charSet = chars.ToString();
            var result = new StringBuilder(options.Length);
            var random = new Random();

            for (int i = 0; i < options.Length; i++)
            {
                result.Append(charSet[random.Next(charSet.Length)]);
            }

            // Ensure at least one character from each selected category
            if (options.IncludeLowercase && !result.ToString().Any(LowercaseChars.Contains))
                result[0] = LowercaseChars[random.Next(LowercaseChars.Length)];

            if (options.IncludeUppercase && !result.ToString().Any(UppercaseChars.Contains))
            {
                int pos = GetRandomPosition(random, options.Length);
                result[pos] = UppercaseChars[random.Next(UppercaseChars.Length)];
            }

            if (options.IncludeDigits && !result.ToString().Any(DigitChars.Contains))
            {
                int pos = GetRandomPosition(random, options.Length);
                result[pos] = DigitChars[random.Next(DigitChars.Length)];
            }

            if (options.IncludeSpecialChars && !result.ToString().Any(SpecialChars.Contains))
            {
                int pos = GetRandomPosition(random, options.Length);
                result[pos] = SpecialChars[random.Next(SpecialChars.Length)];
            }

            return result.ToString();
        }

        private static int GetRandomPosition(Random random, int length)
        {
            return random.Next(0, length - 1);
        }

        public static string GenerateSimplePassword(int length = 12)
        {
            var options = new PasswordGeneratorOptions
            {
                Length = length,
                IncludeUppercase = true,
                IncludeLowercase = true,
                IncludeDigits = true,
                IncludeSpecialChars = false
            };
            return GeneratePassword(options);
        }

        public static bool ValidatePasswordStrength(string password, out string message)
        {
            message = string.Empty;

            if (password.Length < 8)
            {
                message = "密码长度至少需要8个字符";
                return false;
            }

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(c => SpecialChars.Contains(c));

            if (!hasUpper)
            {
                message = "密码必须包含大写字母";
                return false;
            }
            if (!hasLower)
            {
                message = "密码必须包含小写字母";
                return false;
            }
            if (!hasDigit)
            {
                message = "密码必须包含数字";
                return false;
            }
            if (!hasSpecial)
            {
                message = "密码必须包含特殊字符";
                return false;
            }

            return true;
        }
    }
}