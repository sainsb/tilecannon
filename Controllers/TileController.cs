using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Web.Mvc;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Tilecannon.Controllers
{
    public class TileController : Controller
    {

        public ActionResult Index()
        {
            return File(Server.MapPath("~/") + "default.htm", "text/html"); 
        }

        public ActionResult ServiceInfo()
        {
            return File(Server.MapPath("~/") + "service.htm", "text/html"); 
        }

        /// <summary>
        /// Just gets a list of available services
        /// hits the database just to get the 'proper' looking name of the service.
        /// </summary>
        /// <returns></returns>
        public ActionResult GetCatalog()
        {
            var catalog = new List<Dictionary<string, string>>();

            foreach (var conn in tilecannon.SqLiteConnections)
            {

                var svc = new Dictionary<string, string>();
                using (var cmd = conn.Value.CreateCommand())
                {
                    svc["service"] = conn.Key;
                    cmd.CommandText = "select value from metadata where name='name'";
                    using (var rdr = cmd.ExecuteReader())
                    {
                        rdr.Read();
                        svc["serviceName"] = rdr.GetString(0);
                    }
                }
                catalog.Add(svc);
            }

            var ESRIServices = Directory.GetDirectories(tilecannon.ESRIPATH);

            foreach(var dir in ESRIServices)
            {
               var svc = new Dictionary<string, string>();
                svc["service"] = Path.GetFileName(dir);
                svc["serviceName"] = Path.GetFileName(dir);
                catalog.Add(svc);
            }

            return Json(catalog, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Tile(string service, int? x, int? y, byte? z)
        {
            return File(tilecannon.SqLiteConnections.ContainsKey(service) ?
                GetTile(service, x, y, z) :
                GetBundleTile(service, (int)x, (int)y, (int)z), "image/png");
        }

        private static byte[] GetTile(string service, int? x, int? y, byte? z)
        {
            //validate input vars
            if (x == null || y == null || z == null)
            {
                return new byte[] { 0 };
            }

            y = IntPow(2, (byte)z) - 1 - y;

            var conn = tilecannon.SqLiteConnections[service];

            using (var cmd = conn.CreateCommand())
            {
                var command = String.Format("select tile_data as t from tiles where zoom_level={0} and tile_column={1} and tile_row={2}", z, x, y);
                cmd.CommandText = command;

                return (byte[])cmd.ExecuteScalar() ?? new byte[] { 0 };
            }
        }

        //Must allow cross-site json for IE - even if it's not cross-site.
        //many hours of my life died to bring us this information

        [AllowCrossSiteJson]
        public ActionResult Grid(string service, int? x, int? y, byte? z)
        {
            //check for callback
            //var c = Request.QueryString["callback"];

            //if (c!="") //something about this conditional isn't perfect
            //{
            //    return Content(c+"("+GetGrid(service, x, y, z)+")", "application/json");

            //}
            return Content(GetGrid(service, x, y, z), "application/json");

        }

        private static string GetGrid(string service, int? x, int? y, byte? z)
        {
            //validate input vars
            if (x == null || y == null || z == null)
            {
                return "{}";
            }

            y = IntPow(2, (byte)z) - 1 - y;

            var conn = tilecannon.SqLiteConnections[service];

            using (var cmd = conn.CreateCommand())
            {
                var command = String.Format("select grid as g from grids where zoom_level={0} and tile_column={1} and tile_row={2}", z, x, y);
                cmd.CommandText = command;

                var b = (byte[]) cmd.ExecuteScalar();

                if(b.Length==0)
                {
                    return "{}";
                }

                var grid = Decompress(b);

                var g = System.Text.Encoding.UTF8.GetString(grid);

                g = g.Substring(0, g.Length - 1);
                g += ", \"data\":{";

                var query = String.Format("SELECT key_name as key, key_json as json from grid_data where zoom_level={0} and tile_column={1} and tile_row={2}", z, x, y);

                using (var keycmd =new SQLiteCommand(query, conn))
                {

                    using (var rdr = keycmd.ExecuteReader())
                    {
                        while(rdr.Read())
                        {
                            g += "\""+ rdr.GetString(0) + "\":" + rdr.GetString(1) + ",";

                        }
                    }
                }

                g = g.Trim(',')+"}}";
                return g;
            }
        }

        public ActionResult TileJson(string service)
        {
            var tilejson = new Dictionary<string, object>();

            tilejson["tilejson"] = "2.0.0";
            tilejson["scheme"] = "xyz";
            
            //Super haxxorz - uses Portland-ish bounds
            if (!tilecannon.SqLiteConnections.ContainsKey(service))
            {
                tilejson["name"] = service;
                tilejson["tiles"] = new string[1]
                                        {"//" + tilecannon.SERVER + "/mbtiles/" + service + "/{z}/{x}/{y}.png"};
                tilejson["minzoom"] = 0;
                tilejson["maxzoom"] = 19;
                tilejson["bounds"] = new double[4]
                                         {-123.125, 45.2921, -122.37, 45.6461};

                return Json(tilejson, JsonRequestBehavior.AllowGet);
            }

            var conn = tilecannon.SqLiteConnections[service];

            using (var metacmd = conn.CreateCommand())
            {
                metacmd.CommandText = "select name, value from metadata";
                using (var rdr = metacmd.ExecuteReader())
                {
                    while(rdr.Read())
                    {
                        if(rdr.GetString(0)=="bounds")
                        {
                            var bounds = new double[4];
                            var x = rdr.GetString(1).Split(',');

                            for(var i=0;i<4;i++)
                            {
                                bounds[i] = Convert.ToDouble(x[i]);
                            }
                            tilejson["bounds"] = bounds;
                        }
                        else if(rdr.GetString(0)=="center")
                        {
                            var cen = rdr.GetString(1).Split(',');
                            var center = new double[3];
                            center[0] = Convert.ToDouble(cen[0]);
                            center[1] = Convert.ToDouble(cen[1]);
                            center[2] = Convert.ToInt16(cen[2]);

                            tilejson["center"] = center;
                        }
                        else if(rdr.GetString(0)=="maxzoom")
                        {
                            tilejson["maxzoom"] = Convert.ToInt16(rdr.GetString(1));
                        }
                        else if (rdr.GetString(0) == "minzoom")
                        {
                            tilejson["minzoom"] = Convert.ToInt16(rdr.GetString(1));
                        }
                        else
                        {
                            tilejson[rdr.GetString(0)] = rdr.GetString(1);
                        }
                    }
                }
            }

            var tiles = new string[1]; 
            tiles[0] = "//"+tilecannon.SERVER+"/mbtiles/" + service + "/{z}/{x}/{y}.png";
            tilejson["tiles"] = tiles;
           
            //check for UTFgrids
            using (var metacmd = conn.CreateCommand())
            {
                try
                {
                    metacmd.CommandText = "select grid_id from grid_key LIMIT 1";
                    using (var rdr = metacmd.ExecuteReader())
                    {
                        rdr.Read();
                        if (rdr.HasRows)
                        {
                            var grids = new string[1];
                            grids[0] = "//"+tilecannon.SERVER+"/mbtiles/" + service + "/{z}/{x}/{y}.json";
                            tilejson["grids"] = grids;
                        }
                    }
                }
                catch(SQLiteException sqlex)
                {
                    
                }
            }
            
            return Json(tilejson, JsonRequestBehavior.AllowGet);

        }

        private static string Pad(string num, int zoom, string type)
        {
            var padding = ((zoom>17 && type=="R") || (zoom>18 && type=="C")) ? 5 : 4;
   
            while(num.Length<padding)
            {
                num = "0" + num;
            }

            return type + num;
        }

        private static byte[] GetBundleTile(string service, int x, int y, int z)
        {
            var zoom = "L" + ((z < 10) ? "0" + z : ""+z);

            var _qe = 1 << z;
            var _ne = (_qe > 128) ? 128 : _qe;

            var bundle_filename_col = Convert.ToInt32(Math.Floor((double)x/_ne)*_ne);
            var bundle_filename_row = Convert.ToInt32(Math.Floor((double)y/_ne)*_ne);
 
            var filename=Pad(bundle_filename_row.ToString("X"),z, "R")+Pad(bundle_filename_col.ToString("X"),z,"C");

            //arcgis bundled cache directory path
            var path = tilecannon.ESRIPATH;

            var bundlxFileName = path + service + @"\Layers\_alllayers\" + zoom + "/" + filename + ".Bundlx";
            var bundleFileName = path + service+@"\Layers\_alllayers\" + zoom + "/" + filename + ".Bundle";

            var col = x - bundle_filename_col;
            var row = y - bundle_filename_row;

            var index = 128 * (col - 0) + (row - 0);

            var isBundlx = new FileStream(bundlxFileName, FileMode.Open, FileAccess.Read, FileShare.Read);

            isBundlx.Seek(16 + 5 * index, 0);

            var buffer = new byte[5];
            isBundlx.Read(buffer, 0, buffer.Length);

            var offset = (buffer[0] & 0xff) + (long)(buffer[1] & 0xff)
            * 256 + (long)(buffer[2] & 0xff) * 65536
            + (long)(buffer[3] & 0xff) * 16777216
            + (buffer[4] & 0xff) * 4294967296L;

            var isBundle = new FileStream(bundleFileName, FileMode.Open, FileAccess.Read, FileShare.Read);

            isBundle.Seek(offset, 0);

            var lengthBytes = new byte[4];
            isBundle.Read(lengthBytes, 0, lengthBytes.Length);

            var length = (lengthBytes[0] & 0xff)
            + (lengthBytes[1] & 0xff) * 256
            + (lengthBytes[2] & 0xff) * 65536
            + (lengthBytes[3] & 0xff) * 16777216;

            var result = new byte[length];
            isBundle.Read(result, 0, result.Length);

            isBundle.Close();
            isBundlx.Close();

            return result;
        }

        private static int IntPow(int x, byte pow)
        {
            var ret = 1;
            while (pow != 0)
            {
                if ((pow & 1) == 1)
                    ret *= x;
                x *= x;
                pow >>= 1;
            }
            return ret;
        }

        private static byte[] Decompress(byte[] zLibCompressedBuffer)
        {
            byte[] resBuffer = null;

            var mInStream = new MemoryStream(zLibCompressedBuffer);
            var mOutStream = new MemoryStream(zLibCompressedBuffer.Length);
            var infStream = new InflaterInputStream(mInStream);

            mInStream.Position = 0;

            try
            {
                var tmpBuffer = new byte[zLibCompressedBuffer.Length];
                var read = 0;

                do
                {
                    read = infStream.Read(tmpBuffer, 0, tmpBuffer.Length);
                    if (read > 0)
                        mOutStream.Write(tmpBuffer, 0, read);

                } while (read > 0);

                resBuffer = mOutStream.ToArray();
            }
            finally
            {
                infStream.Close();
                mInStream.Close();
                mOutStream.Close();
            }

            return resBuffer;
        }
    }

    public class AllowCrossSiteJsonAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            filterContext.RequestContext.HttpContext.Response.AddHeader("Access-Control-Allow-Origin", "*");
            base.OnActionExecuting(filterContext);
        }
    }
}