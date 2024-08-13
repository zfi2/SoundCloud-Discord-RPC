using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Newtonsoft.Json.Linq;

// DiscordIPC.cs
namespace DiscordRichPresence
{
    public class Presence : IDisposable
    {
        private readonly string _clientId;
        private readonly WindowsPipe _pipe;

        /// <summary>
        /// Initializes a new instance of the <see cref="Presence"/> class.
        /// </summary>
        /// <param name="clientId">The client ID for the Discord Rich Presence application.</param>
        /// <remarks>
        /// This constructor initializes the Presence object, creates a new WindowsPipe,
        /// and performs the initial handshake with the Discord client.
        /// </remarks>
        public Presence(string clientId)
        {
            _clientId = clientId;
            _pipe = new WindowsPipe();
            Handshake();
        }

        /// <summary>
        /// Sets the Discord Rich Presence activity for the current application.
        /// </summary>
        /// <param name="activityJson">A JSON string representing the activity to be set. 
        /// This should conform to the Discord Rich Presence activity structure.</param>
        /// <remarks>
        /// This method sends the activity data to Discord, updates the user's presence,
        /// and handles any errors that may occur during the process.
        /// </remarks>
        /// <exception cref="ActivityException">Thrown when there's an error with the activity data (error code 4000).</exception>
        /// <exception cref="PresenceException">Thrown for any other error during the presence update process.</exception>
        public void Set(string activityJson)
        {
            var payload = new JObject
            {
                ["cmd"] = "SET_ACTIVITY",
                ["args"] = new JObject
                {
                    ["pid"] = Process.GetCurrentProcess().Id,
                    ["activity"] = JObject.Parse(activityJson)
                },
                ["nonce"] = Guid.NewGuid().ToString()
            };

            Send(payload.ToString(), OpCode.Frame);

            var reply = Read();
            if (reply.TryGetValue("evt", out var evt) && evt.ToString() == "ERROR")
            {
                var data = reply["data"];
                var message = data["message"].ToString();
                var code = (int)data["code"];

                if (code == 4000)
                {
                    const string prefix = "child \"activity\" fails because [";
                    if (message.StartsWith(prefix))
                    {
                        message = message.Substring(prefix.Length, message.Length - prefix.Length - 1);
                    }
                    throw new ActivityException(message);
                }

                throw new PresenceException(message, code);
            }
        }

        /// <summary>
        /// Clears the current Discord Rich Presence activity.
        /// </summary>
        /// <remarks>
        /// This method sets an empty activity, effectively clearing the current presence.
        /// </remarks>
        public void Clear() => Set("{}");

        /// <summary>
        /// Closes the connection to the Discord client and releases associated resources.
        /// </summary>
        /// <remarks>
        /// This method sends a close operation to Discord and ensures that the pipe connection is closed,
        /// even if an exception occurs during the sending process.
        /// </remarks>
        public void Close()
        {
            try
            {
                Send("{}", OpCode.Close);
            }
            finally
            {
                _pipe.Close();
            }
        }

        /// <summary>
        /// Performs the initial handshake with the Discord client.
        /// </summary>
        /// <remarks>
        /// This method sends a handshake payload to the Discord client with the version and client ID,
        /// then reads and processes the response. If the handshake is successful, the method completes
        /// without throwing an exception. If there are any issues during the handshake, appropriate
        /// exceptions are thrown.
        /// </remarks>
        /// <exception cref="ClientIDException">Thrown when the client ID is invalid (error code 4000).</exception>
        /// <exception cref="PresenceException">Thrown when there's an unexpected error during the handshake process.</exception>
        private void Handshake()
        {
            Send(new JObject
            {
                ["v"] = 1,
                ["client_id"] = _clientId
            }.ToString(), OpCode.Handshake);

            var payload = Read();

            if (!payload.TryGetValue("evt", out var evt) || evt.ToString() != "READY")
            {
                if (payload.TryGetValue("code", out var codeElement))
                {
                    var code = (int)codeElement;
                    if (code == 4000)
                    {
                        throw new ClientIDException();
                    }
                    var message = payload.TryGetValue("message", out var msgElement) ? msgElement.ToString() : "Unknown error";
                    throw new PresenceException(message, code);
                }
                throw new PresenceException("Unexpected handshake response", 0);
            }
        }

        /// <summary>
        /// Reads and parses a JSON payload from the Discord IPC pipe.
        /// </summary>
        /// <remarks>
        /// This method first reads the header to determine the operation code and payload length,
        /// then reads the payload data, converts it to a string, and finally parses it into a JObject.
        /// </remarks>
        /// <returns>
        /// A JObject representing the parsed JSON payload received from Discord.
        /// </returns>
        private JObject Read()
        {
            var (op, length) = ReadHeader();
            var data = _pipe.Read(length);
            var json = Encoding.UTF8.GetString(data);
            return JObject.Parse(json);
        }

        /// <summary>
        /// Reads the header from the Discord IPC pipe.
        /// </summary>
        /// <remarks>
        /// This method reads 8 bytes from the pipe, which represent the header of a Discord IPC message.
        /// The header consists of two 32-bit integers: the operation code and the length of the payload.
        /// </remarks>
        /// <returns>
        /// A tuple containing two integers:
        /// <list type="bullet">
        /// <item><description>Op: The operation code of the message.</description></item>
        /// <item><description>Length: The length of the payload in bytes.</description></item>
        /// </list>
        /// </returns>
        private (int Op, int Length) ReadHeader()
        {
            var headerData = _pipe.Read(8);
            return (BitConverter.ToInt32(headerData, 0), BitConverter.ToInt32(headerData, 4));
        }

        /// <summary>
        /// Sends a payload to the Discord client through the IPC pipe.
        /// </summary>
        /// <param name="payload">The JSON string to be sent as the payload.</param>
        /// <param name="op">The operation code indicating the type of message being sent.</param>
        /// <remarks>
        /// This method constructs the message by creating a header containing the operation code and payload length,
        /// then writes both the header and the payload data to the pipe.
        /// </remarks>
        private void Send(string payload, OpCode op)
        {
            var data = Encoding.UTF8.GetBytes(payload);
            var header = new byte[8];
            BitConverter.GetBytes((int)op).CopyTo(header, 0);
            BitConverter.GetBytes(data.Length).CopyTo(header, 4);
            _pipe.Write(header);
            _pipe.Write(data);
        }

        /// <summary>
        /// Disposes of the Presence object, releasing any resources it holds.
        /// </summary>
        /// <remarks>
        /// This method calls the <see cref="Close"/> method to ensure proper cleanup of resources,
        /// including closing the connection to the Discord client and releasing the pipe connection.
        /// </remarks>
        public void Dispose() => Close();
    }

    public class PresenceException : Exception
    {
        public int Code { get; }

        public PresenceException(string message, int code) : base(message)
        {
            Code = code;
        }
    }

    public class ClientIDException : PresenceException
    {
        public ClientIDException() : base("Client ID is invalid", 4000) { }
    }

    public class ActivityException : PresenceException
    {
        public ActivityException(string message) : base(message, 4000) { }
    }

    internal enum OpCode
    {
        Handshake = 0,
        Frame = 1,
        Close = 2,
        Ping = 3,
        Pong = 4
    }

    internal class WindowsPipe
    {
        private readonly NamedPipeClientStream _pipeClient;

        /// <summary>
        /// Initializes a new instance of the WindowsPipe class.
        /// </summary>
        /// <remarks>
        /// This constructor attempts to establish a connection to a Discord IPC pipe.
        /// It tries up to 10 different pipe names (discord-ipc-0 to discord-ipc-9) before giving up.
        /// </remarks>
        /// <exception cref="FileNotFoundException">
        /// Thrown when no suitable Discord IPC pipe can be found after 10 attempts.
        /// </exception>
        public WindowsPipe()
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    _pipeClient = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.None);
                    _pipeClient.Connect(1000);
                    return;
                }
                catch (TimeoutException) { }
            }
            throw new FileNotFoundException("Cannot find a Windows named pipe to connect to Discord");
        }

        /// <summary>
        /// Reads a specified number of bytes from the named pipe.
        /// </summary>
        /// <param name="size">The number of bytes to read from the pipe.</param>
        /// <returns>A byte array containing the read data.</returns>
        /// <remarks>
        /// This method will continue reading from the pipe until it has read the specified number of bytes.
        /// If the pipe doesn't have enough data immediately available, it will block until all requested data is read.
        /// </remarks>
        public byte[] Read(int size)
        {
            var buffer = new byte[size];
            int bytesRead = 0;
            while (bytesRead < size)
            {
                bytesRead += _pipeClient.Read(buffer, bytesRead, size - bytesRead);
            }
            return buffer;
        }

        /// <summary>
        /// Writes the specified byte array to the named pipe.
        /// </summary>
        /// <param name="data">The byte array to write to the pipe.</param>
        /// <remarks>
        /// This method writes all bytes in the provided array to the pipe in a single operation.
        /// </remarks>
        public void Write(byte[] data) => _pipeClient.Write(data, 0, data.Length);

        /// <summary>
        /// Closes the named pipe connection.
        /// </summary>
        /// <remarks>
        /// This method should be called when the pipe is no longer needed to release system resources.
        /// </remarks>
        public void Close() => _pipeClient.Close();
    }
}