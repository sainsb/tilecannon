tilecannon
==========

![alt tag](http://www.guerrillagis.net/tilecannon.png)

An IIS (ASP.NET MVC) tileserver for mbtiles, UTFGrids and ESRI bundled cache

* At my agency we do a lot of caching.
* We write code in Python and C#.NET
* We mainly use ArcGIS Server to create caches (lots of air photos)
* We utilize bundled cache.
* We run Windows web servers and IIS.
* ArcGIS Server is mostly super overkill.
* We like Tilemill, Mapbox, Mapnik, Carto CSS, UTFGrids etc.

To this end, I've written this tileserver to be a swiss army knife for serving 
 mbtiles, UTFGrids, and ESRI bundled cache.

Todo
----
* Get some meta setup for tilejson requests for bundled cache.
* support exploded cache without having to check on each tile
* Scrub out non-Web Merc bundled cache services.
* Support a different dataframe name (e.g most of the time it's "Layers", but not always)
* Implement WMTS specification.

FAQ
---

* # How do I set this thing up?

In the global.asax change the directory pointers to where you keep your mbtiles files, your bundled cache, and the name of your host. Then push the big green 'GO' button.
