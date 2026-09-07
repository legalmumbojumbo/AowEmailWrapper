using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using ComponentAce.Compression.Libs.zlib;
using AowEmailWrapper.Helpers;

namespace AowEmailWrapper.ASG
{
	public class DataCompressor
	{
		/// <summary>
		/// Largest inflated size accepted. Compressed data can expand a thousandfold, so without
		/// a ceiling a small crafted attachment could take all the memory the process can get.
		/// </summary>
		public const long MaxDecompressedBytes = 64L * 1024 * 1024;

		public DataCompressor (BinaryReader input, long length, bool compressed)
		{
			_compressed = compressed;
			Data = input.ReadBytes((int)length);
		}

		public DataCompressor (BinaryReader input, bool compressed)
			: this(input, input.BaseStream.Length - input.BaseStream.Position, compressed)
		{
		}

		public byte[] Data
		{
			get;
			private set;
		}

		public bool Compressed
		{
			get
			{
				return _compressed;
			}
			set
			{
				if ( value != _compressed )
					if ( value )
						Data = Compress(Data);
					else
						Data = Decompress(Data);

				_compressed = value;
			}
		}

		private bool _compressed;

		private byte[] Compress (byte[] data)
		{
			MemoryStream compressed_out = new MemoryStream();
			ZOutputStream z_compressor_stream = new ZOutputStream(compressed_out, zlibConst.Z_DEFAULT_COMPRESSION);

			z_compressor_stream.Write(data, 0, data.Length);
			z_compressor_stream.Close();
			byte[] result = compressed_out.ToArray();

			compressed_out.Close();

			return result;
		}

		private byte[] Decompress (byte[] data)
		{
			//Throws InvalidDataException once the output passes the limit, which stops the inflate at once
			using (BoundedMemoryStream decompressed_out = new BoundedMemoryStream(MaxDecompressedBytes))
			{
				ZOutputStream z_decompressor_stream = new ZOutputStream(decompressed_out);

				z_decompressor_stream.Write(data, 0, data.Length);
				z_decompressor_stream.Close();

				return decompressed_out.ToArray();
			}
		}
	}
}
