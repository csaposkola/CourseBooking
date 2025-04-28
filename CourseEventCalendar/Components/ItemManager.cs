using System.Collections.Generic;
using CourseEventCalendar.CourseEventCalendar.Models;

namespace CourseEventCalendar.CourseEventCalendar.Components
{
    public class ItemManager
    {
        public static ItemManager Instance = new ItemManager();
        
        public Models.Item GetItem(int itemId, int moduleId)
        {
            return null;
        }
        
        public void CreateItem(Models.Item item)
        {
        }
        
        public void UpdateItem(Models.Item item)
        {
        }
        
        public void DeleteItem(int itemId, int moduleId)
        {
        }
        
        public IEnumerable<Models.Item> GetItems(int moduleId)
        {
            return new List<Models.Item>();
        }
    }
}