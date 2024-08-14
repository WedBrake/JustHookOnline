using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Mirror.SimpleWeb
{
    /// <summary>
    /// Handles Handshake to the server when it first connects
    /// <para>The client handshake does not need buffers to reduce allocations since it only happens once</para>
    /// </summary>
    internal class ClientHandshake
    {
        public bool TryHandshake(Connection conn, Uri uri)
        {
            try
            {
                var stream = conn.stream;

                var keyBuffer = new byte[16];
                using (var rng = new RNGCryptoServiceProvider())
                    rng.GetBytes(keyBuffer);

                var key = Convert.ToBase64String(keyBuffer);
                var keySum = key + Constants.HandshakeGUID;
                var keySumBytes = Encoding.ASCII.GetBytes(keySum);
                Log.Verbose($"[SWT-ClientHandshake]: Handshake Hashing {Encoding.ASCII.GetString(keySumBytes)}");

                // SHA-1 is the websocket standard:
                // https://www.rfc-editor.org/rfc/rfc6455
                // we should follow the standard, even though SHA1 is considered weak:
                // https://stackoverflow.com/questions/38038841/why-is-sha-1-considered-insecure
                var keySumHash = SHA1.Create().ComputeHash(keySumBytes);

                var expectedResponse = Convert.ToBase64String(keySumHash);
                var handshake =
                    $"GET {uri.PathAndQuery} HTTP/1.1\r\n" +
                    $"Host: {uri.Host}:{uri.Port}\r\n" +
                    $"Upgrade: websocket\r\n" +
                    $"Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Key: {key}\r\n" +
                    $"Sec-WebSocket-Version: 13\r\n" +
                    "\r\n";
                var encoded = Encoding.ASCII.GetBytes(handshake);
                stream.Write(encoded, 0, encoded.Length);

                var responseBuffer = new byte[1000];

                var lengthOrNull = ReadHelper.SafeReadTillMatch(stream, responseBuffer, 0, responseBuffer.Length, Constants.endOfHandshake);

                if (!lengthOrNull.HasValue)
                {
                    Log.Error("[SWT-ClientHandshake]: Connection closed before handshake");
                    return false;
                }

                var responseString = Encoding.ASCII.GetString(responseBuffer, 0, lengthOrNull.Value);
                Log.Verbose($"[SWT-ClientHandshake]: Handshake Response {responseString}");

                var acceptHeader = "Sec-WebSocket-Accept: ";
                var startIndex = responseString.IndexOf(acceptHeader, StringComparison.InvariantCultureIgnoreCase);

                if (startIndex < 0)
                {
                    Log.Error($"[SWT-ClientHandshake]: Unexpected Handshake Response {responseString}");
                    return false;
                }

                startIndex += acceptHeader.Length;
                var endIndex = responseString.IndexOf("\r\n", startIndex);
                var responseKey = responseString.Substring(startIndex, endIndex - startIndex);

                if (responseKey != expectedResponse)
                {
                    Log.Error($"[SWT-ClientHandshake]: Response key incorrect\n" +
                        $"Expected:{expectedResponse}\n" +
                        $"Response:{responseKey}\n" +
                        $"This can happen if Websocket Protocol is not installed in Windows Server Roles.");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Exception(e);
                return false;
            }
        }
    }
}
