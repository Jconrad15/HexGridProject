using System.Collections.Generic;

namespace TheZooMustGrow
{
	public class HexCellPriorityQueue
	{

		List<HexCell> list = new List<HexCell>();

		int minimum = int.MaxValue;

		int count = 0;
		public int Count
		{
			get
			{
				return count;
			}
		}


		public void Enqueue(HexCell cell)
		{
			count += 1;
			int priority = cell.SearchPriority;
			
			if (priority < minimum)
            {
				minimum = priority;
            }

			// Add dummy null elements until list has lenght == priority
			while (priority >= list.Count)
			{
				list.Add(null);
			}

			cell.NextWithSamePriority = list[priority];
			list[priority] = cell;
		}

		public HexCell Dequeue()
		{
			count -= 1;
			for (; minimum < list.Count; minimum++)
			{
				HexCell cell = list[minimum];
				if (cell != null)
				{
					// If a cell is found and returned, bump up the
					// chained list if a next cell exists, otherwise this 
					// provides a null value for the list at this priority level.
					list[minimum] = cell.NextWithSamePriority;
					return cell;
				}
			}

			return null;
		}

		public void Change(HexCell cell, int oldPriority)
		{
			HexCell current = list[oldPriority];
			HexCell next = current.NextWithSamePriority;

			// If the changed cell is at the head of the chained list
			if (current == cell)
			{
				list[oldPriority] = next;
			}
			else
			{
				while (next != cell)
				{
					current = next;
					next = current.NextWithSamePriority;
				}
				// At this point, found changed cell
				current.NextWithSamePriority = cell.NextWithSamePriority;
			}

			// Re-enqueue the changed cell
			Enqueue(cell);
			count -= 1;

		}

		public void Clear()
		{
			list.Clear();
			count = 0;
			minimum = int.MaxValue;
		}
	}
}