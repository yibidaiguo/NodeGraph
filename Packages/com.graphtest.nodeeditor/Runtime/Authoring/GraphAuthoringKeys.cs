// GraphAuthoringKeys.cs —— authoringKey 的纯层验证与确定性旧资产回填。

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NodeEditor
{
    internal static class GraphAuthoringKeys
    {
        const string Base32Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

        internal static bool IsValid(string key) =>
            !string.IsNullOrEmpty(key) && key == key.Trim() && !key.Any(char.IsControl);

        internal static bool TryCreateLegacyKey(string instanceId, ISet<string> usedKeys, out string key)
        {
            key = null;
            var hash = Base32Sha256(instanceId);
            for (int length = 12; length <= hash.Length; length += 4)
            {
                var candidate = "n-" + hash.Substring(0, length);
                if (!usedKeys.Add(candidate)) continue;
                key = candidate;
                return true;
            }
            return false;
        }

        static string Base32Sha256(string value)
        {
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var output = new StringBuilder((hash.Length * 8 + 4) / 5);
            var buffer = 0;
            var bits = 0;
            foreach (var b in hash)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    output.Append(Base32Alphabet[(buffer >> bits) & 31]);
                }
            }
            if (bits > 0) output.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
            return output.ToString();
        }
    }
}
