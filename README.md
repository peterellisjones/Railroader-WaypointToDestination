# Railroader Mod: WaypointToDestination

![](./Capture.PNG)

This mod adds a button to the freight car operations tab that will route the train to the desitnation track.

To work sucessfully there **should be one and only one locomotive in the consist that does not have its brakes cut out** -- this will be the locomotive that the waypoint order is given to.

Caveats:
* When there are multiple possible destination tracks, like at Sylvia Interchange, it doesn't know which is the best one to go to, it will just choose the first one it finds.
* It will route the train to whichever end of the destination track is furthest away. Usually this means the end of the track if the track is a dead-end. If there are cars in the way, then it will approach them but not couple.

## Installation

* Download `WaypointToDestination-VERSION.Railloader.zip` from the releases page
* Install with [Railloader](https://railroader.stelltis.ch/)
