using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public class Deque<T>
	{
		private readonly int capacity_;
		private readonly T[] ringBuffer_;
		private int front_;
		private int back_;

		public int Size { get; private set; }

		public T this[int index]
		{
			get
			{
				return ringBuffer_[(index + front_) % capacity_];
			}
		}

		public Deque(int capacity)
		{
			capacity_ = capacity;
			ringBuffer_ = new T[capacity];
		}

		public void PushFront(T item)
		{
			if (Size >= capacity_) {
				return;
			}

			if (Size == 0) {
				ringBuffer_[0] = item;
				front_ = 0;
				back_ = 0;
			} else {
				int index = (front_ + capacity_ - 1) % capacity_;
				ringBuffer_[index] = item;
				front_ = index;
			}

			Size++;
		}

		public void PushBack(T item)
		{
			if (Size >= capacity_) {
				return;
			}

			if (Size == 0) {
				ringBuffer_[0] = item;
				front_ = 0;
				back_ = 0;
			} else {
				int index = (back_ + 1) % capacity_;
				ringBuffer_[index] = item;
				back_ = index;
			}

			Size++;
		}

		public T PopFront()
		{
			if (Size == 0) {
				return default(T);
			}

			T ret = ringBuffer_[front_];
			int index = (front_ + 1) % capacity_;
			front_ = index;
			Size--;

			return ret;
		}

		public T PopBack()
		{
			if (Size == 0) {
				return default(T);
			}

			T ret = ringBuffer_[back_];
			int index = (back_ + capacity_ - 1) % capacity_;
			back_ = index;
			Size--;

			return ret;
		}
	}
}
