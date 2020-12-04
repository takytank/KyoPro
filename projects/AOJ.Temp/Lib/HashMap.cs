using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public class HashMap<TKey, TValue> : Dictionary<TKey, TValue>
	{
		private readonly TValue default_;
		public HashMap(TValue defaultValue)
			: base()
		{
			default_ = defaultValue;
		}

		public HashMap(TValue defaultValue, int capacity)
			: base(capacity)
		{
			default_ = defaultValue;
		}

		new public TValue this[TKey key]
		{
			get
			{
				if (ContainsKey(key) == false) {
					base[key] = default_;
				}

				return base[key];
			}

			set { base[key] = value; }
		}
	}
}
