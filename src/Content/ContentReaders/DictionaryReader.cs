#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

#region Using Statements
using System;
using System.Collections.Generic;
#endregion

namespace Microsoft.Xna.Framework.Content
{
	internal class DictionaryReader<TKey, TValue> : ContentTypeReader<Dictionary<TKey, TValue>>
	{
		#region Private Variables

		ContentTypeReader keyReader;
		ContentTypeReader valueReader;

		Type keyType;
		Type valueType;

		#endregion

		#region Public Constructor

		public DictionaryReader()
		{
		}

		#endregion

		#region Protected Initialization Method

		protected internal override void Initialize(ContentTypeReaderManager manager)
		{
			keyType = typeof(TKey);
			valueType = typeof(TValue);
			keyReader = manager.GetTypeReader(keyType);
			valueReader = manager.GetTypeReader(valueType);
		}

		#endregion

		#region Protected Read Method

		protected internal override Dictionary<TKey, TValue> Read(ContentReader input, Dictionary<TKey, TValue> existingInstance)
		{
			int count = input.ReadInt32();
			Dictionary<TKey, TValue> dictionary = existingInstance;
			if (dictionary == null)
			{
				dictionary = new Dictionary<TKey, TValue>(count);
			}
			else
			{
				dictionary.Clear();
			}

			for (int i = 0; i < count; i += 1)
			{
				TKey key;
				TValue value;
				if (keyType.IsValueType)
				{
					key = input.ReadObject<TKey>(keyReader);
				}
				else
				{
					int readerType = input.ReadByte();
					key = input.ReadObject<TKey>(input.TypeReaders[readerType - 1]);
				}
				if (valueType.IsValueType)
				{
					value = input.ReadObject<TValue>(valueReader);
				}
				else
				{
					int readerType = input.ReadByte();
					value = input.ReadObject<TValue>(input.TypeReaders[readerType - 1]);
				}
				dictionary.Add(key, value);
			}
			return dictionary;
		}

		#endregion
	}
}

