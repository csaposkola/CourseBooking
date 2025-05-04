// Components/CourseEventManager.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using Csaposkola.Modules.Kurzusnaptar.Models;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Framework;

namespace Csaposkola.Modules.Kurzusnaptar.Components
{
    internal interface ICourseEventManager
    {
        IEnumerable<CourseEvent> GetCourseEvents();
    }

    internal class CourseEventManager : ServiceLocator<ICourseEventManager, CourseEventManager>, ICourseEventManager
    {
        public IEnumerable<CourseEvent> GetCourseEvents()
        {
            var courses = new List<CourseEvent>();
            string connectionString = Config.GetConnectionString();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT 
                        p.bvin AS ProductId,
                        pt.ProductName,
                        p.Sku,
                        p.SitePrice,
                        COALESCE(SUM(i.QuantityOnHand), 0) AS InventoryCount,
                        p.RewriteUrl AS ProductLink
                    FROM 
                        hcc_Product p
                    JOIN 
                        hcc_ProductXCategory pc ON p.bvin = pc.ProductId
                    LEFT JOIN 
                        hcc_ProductInventory i ON p.bvin = i.ProductBvin
                    LEFT JOIN
                        hcc_ProductTranslations pt ON p.bvin = pt.ProductId
                    WHERE 
                        pc.CategoryId = '5FA44073-DF1D-4087-8EC6-28043C7BC360'
                    GROUP BY 
                        p.bvin, pt.ProductName, p.Sku, p.SitePrice, p.RewriteUrl
                    ORDER BY 
                        pt.ProductName";

                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var course = new CourseEvent
                        {
                            ProductId = reader["ProductId"].ToString(),
                            ProductName = reader["ProductName"].ToString(),
                            Sku = reader["Sku"].ToString(),
                            SitePrice = Convert.ToDecimal(reader["SitePrice"]),
                            InventoryCount = Convert.ToInt32(reader["InventoryCount"]),
                            ProductLink = reader["ProductLink"].ToString()
                        };

                        // Parse date and duration from SKU
                        ParseSkuData(course);

                        courses.Add(course);
                    }
                }
            }

            return courses;
        }

        private void ParseSkuData(CourseEvent course)
        {
            // Example SKU: TANF-ALAPSORF-4H-202505100058
            var durationMatch = Regex.Match(course.Sku, @"-(\d+)H-");
            if (durationMatch.Success)
            {
                course.DurationHours = int.Parse(durationMatch.Groups[1].Value);
            }

            var dateMatch = Regex.Match(course.Sku, @"-(\d{12})$");
            if (dateMatch.Success)
            {
                string dateStr = dateMatch.Groups[1].Value;
                // Format: YYYYMMDDHHMM
                int year = int.Parse(dateStr.Substring(0, 4));
                int month = int.Parse(dateStr.Substring(4, 2));
                int day = int.Parse(dateStr.Substring(6, 2));
                int hour = int.Parse(dateStr.Substring(8, 2));
                int minute = int.Parse(dateStr.Substring(10, 2));

                course.StartDate = new DateTime(year, month, day, hour, minute, 0);
            }
        }

        protected override System.Func<ICourseEventManager> GetFactory()
        {
            return () => new CourseEventManager();
        }
    }
}