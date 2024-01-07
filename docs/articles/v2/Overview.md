## Quickstart
*See [here](../v1/Overview.md) for the PrefabsV1 API documentation.*  
It contains both the v1 API usage and how to generally register UIExtenderEx to make it work!  

Version 2 of the API builds off of the the concepts of the original API, but offers a bit more versatility, and aggregates some of the original prefab types to offer a (hopefully) simpler API.

All UIExtenderEx patch classes must be flagged with a [``PrefabExtensionAttribute``](xref:Bannerlord.UIExtenderEx.Attributes.PrefabExtensionAttribute). 
The first parameter is the name of the Movie (the name of the xml file) that your patch targets.
The second parameter is an ``XPath`` used to specify the node you wish to target inside of the targetted Movie.

**For those of you who are unfamiliar with XPath:**
- [Tutorial](https://www.w3schools.com/xml/xpath_intro.asp)
- [Cheatsheet](https://devhints.io/xpath)
