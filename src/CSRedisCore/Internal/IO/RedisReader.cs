﻿using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CSRedis.Internal.IO
{
	internal partial class RedisReader
    {
        readonly RedisIO _io;

        public RedisReader(RedisIO io)
        {
            _io = io;
        }

        public RedisMessage ReadType()
        {
            RedisMessage type = (RedisMessage)_io.ReadByte();
            //Console.WriteLine($"ReadType: {type}");
            if (type == RedisMessage.Error)
                throw new RedisException(ReadStatus(false));
            return type;
        }

        public string ReadStatus(bool checkType = true)
        {
            if (checkType)
                ExpectType(RedisMessage.Status);
            return ReadLine();
        }

        public long ReadInt(bool checkType = true)
        {
            if (checkType)
                ExpectType(RedisMessage.Int);

            string line = ReadLine();
            return Int64.Parse(line.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat);
        }

        public object ReadBulk(bool checkType = true, bool asString = false)
        {
            if (asString)
                return ReadBulkString(checkType);
            else
                return ReadBulkBytes(checkType);
        }

        public byte[] ReadBulkBytes(bool checkType = true)
        {
            if (checkType)
                ExpectType(RedisMessage.Bulk);

            int size = (int)ReadInt(false);
            if (size == -1)
                return null;

            byte[] bulk = new byte[size];
            int bytes_read = 0;
            int bytes_remaining = size;

            while (bytes_read < size)
                bytes_read += _io.Read(bulk, bytes_read, size - bytes_read);

            //Console.WriteLine($"ReadBulkBytes1: {Encoding.UTF8.GetString(bulk)}");
            ExpectBytesRead(size, bytes_read);
            ReadCRLF();
            return bulk;
        }

        public void ReadBulkBytes(Stream destination, int bufferSize, bool checkType = true)
        {
            if (checkType)
                ExpectType(RedisMessage.Bulk);
            int size = (int)ReadInt(false);
            if (size == -1)
                return;

            byte[] buffer = new byte[bufferSize];
            int position = 0;
            while (position < size)
            {
                int bytes_to_buffer = Math.Min(buffer.Length, size - position);
                int bytes_read = 0;
                while (bytes_read < bytes_to_buffer)
                {
                    int bytes_to_read = Math.Min(bytes_to_buffer - bytes_read, size - position);
                    bytes_read += _io.Read(buffer, bytes_read, bytes_to_read);
                }
                position += bytes_read;
                destination.Write(buffer, 0, bytes_read);
            }
            //Console.WriteLine($"ReadBulkBytes2: {Encoding.UTF8.GetString(buffer)}");
            ExpectBytesRead(size, position);
            ReadCRLF();
        }

        public string ReadBulkString(bool checkType = true)
        {
            byte[] bulk = ReadBulkBytes(checkType);
            if (bulk == null)
                return null;
            return _io.Encoding.GetString(bulk);
        }

        public void ExpectType(RedisMessage expectedType)
        {
            RedisMessage type = ReadType();
            if ((int)type == -1)
            {
                var alldata = _io.ReadAll();
                try { _io.Dispose(); } catch { }
                throw new EndOfStreamException($"Unexpected end of stream; expected type '{expectedType}'; data = '{Encoding.UTF8.GetString(alldata)}'");
            }
            if (type != expectedType)
                throw new RedisProtocolException($"Unexpected response type: {type} (expecting {expectedType})");
        }

        public void ExpectMultiBulk(long expectedSize)
        {
            ExpectType(RedisMessage.MultiBulk);
            ExpectSize(expectedSize);
        }

        public void ExpectSize(long expectedSize)
        {
            long size = ReadInt(false);
            ExpectSize(expectedSize, size);
        }

        public void ExpectSize(long expectedSize, long actualSize)
        {
            if (actualSize != expectedSize)
                throw new RedisProtocolException("Expected " + expectedSize + " elements; got " + actualSize);
        }

        public void ReadCRLF() // TODO: remove hardcoded
        {
            var r = _io.ReadByte();
            var n = _io.ReadByte();
            //Console.WriteLine($"ReadCRLF: {r} {n}");
            if (r != (byte)13 && n != (byte)10)
                throw new RedisProtocolException(String.Format("Expecting CRLF; got bytes: {0}, {1}", r, n));
        }

        public object[] ReadMultiBulk(bool checkType = true, bool bulkAsString = false)
        {
            if (checkType)
                ExpectType(RedisMessage.MultiBulk);
            long count = ReadInt(false);
            if (count == -1)
                return null;

            object[] lines = new object[count];
            for (int i = 0; i < count; i++)
                lines[i] = Read(bulkAsString);
            return lines;
        }

        public object Read(bool bulkAsString = false)
        {
            RedisMessage type = ReadType();
            switch (type)
            {
                case RedisMessage.Bulk:
                    return ReadBulk(false, bulkAsString);

                case RedisMessage.Int:
                    return ReadInt(false);

                case RedisMessage.MultiBulk:
                    return ReadMultiBulk(false, bulkAsString);

                case RedisMessage.Status:
                    return ReadStatus(false);

                case RedisMessage.Error:
                    throw new RedisException(ReadStatus(false));

                default:
                    throw new RedisProtocolException("Unsupported response type: " + type);
            }
        }

        string ReadLine()
        {
			if (!_io.Stream.CanRead)
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder();
			bool flag = false;
			int c = -1;
			//Judgment _io.ReadByte() of return -1
			while ((c = _io.ReadByte()) != -1)
			{
				if ((char)c == '\r')
				{
					flag = true;
					continue;
				}
				//Judgment '\r\n',or only '\n'
				if (((char)c == '\n') || ((char)c == '\n' && flag))
				{
					break;
				}
				stringBuilder.Append((char)c);
				flag = false;
			}
			return stringBuilder.ToString();
        }

        void ExpectBytesRead(long expecting, long actual)
        {
            if (actual != expecting)
                throw new RedisProtocolException(String.Format("Expecting {0} bytes; got {1} bytes", expecting, actual));
        }
    }
}
