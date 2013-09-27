using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Data.SQLite;

namespace Tilecannon
{

    public class tilecannon : HttpApplication
    {

        public static string TILEPATH = "C:/mbtiles/";
        public static string ESRIPATH = @"\\alex\agfs\arcgisserver\directories\arcgiscache\";
        public static string SERVER = "localhost";

        public static Dictionary<string, SQLiteConnection> SqLiteConnections;

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "Tiles", // Route name
                "{service}/{z}/{x}/{y}.png", // URL with parameters
                new { controller = "Tile", action = "Tile"} // Parameter defaults
            );

            routes.MapRoute(
                "GetBundleTiles", // Route name
                "{service}/{z}/{x}/{y}.bung", // URL with parameters
                new { controller = "Tile", action = "GetBundleTile" } // Parameter defaults
            );

            routes.MapRoute(
                "TileJson", // Route name
                "{service}/tilejson/", // URL with parameters
                new { controller = "Tile", action = "TileJson" } // Parameter defaults
            );

            routes.MapRoute(
                "Grids", // Route name
                "{service}/{z}/{x}/{y}.json", // URL with parameters
                new { controller = "Tile", action = "Grid" } // Parameter defaults
            );

            routes.MapRoute(
                "Default", // Route name
                "", // URL with parameters
                new { controller = "Tile", action = "Index" } // Parameter defaults
            );

            routes.MapRoute(
            "IndexDataService", // Route name
            "GetCatalog", // URL with parameters
            new { controller = "Tile", action = "GetCatalog" } // Parameter defaults
         );

            routes.MapRoute(
             "Service", // Route name
             "{service}/", // URL with parameters
             new { controller = "Tile", action = "ServiceInfo" } // Parameter defaults
         );
        }

        protected void Application_Start()
        {
            //Mainline the connections
            //Oh yeah!

            SqLiteConnections = new Dictionary<string, SQLiteConnection>();
            var mbtiles = Directory.GetFiles(TILEPATH);

            foreach (var file in mbtiles)
            {
                SqLiteConnections.Add(Path.GetFileNameWithoutExtension(file), new SQLiteConnection(@"Data Source="+file));
            }

            foreach(var conn in SqLiteConnections.Values)
            {
                conn.Open();
            }

            AreaRegistration.RegisterAllAreas();

            RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_End()
        {
            foreach (var conn in SqLiteConnections.Values)
            {
                conn.Close();
                conn.Dispose();
            }
        }
    }
}