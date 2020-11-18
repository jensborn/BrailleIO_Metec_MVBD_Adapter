# BrailleIO_Metec_MVBD_Adapter
An IBrailleIOAdapter implementation for the mectec braille devices, accessible through the MVBD.

The BrailleIO framework allows for building applications for planar refershable multiline braille-displays. It can be found on github on  

[To the BrailleIO project on github](https://github.com/TUD-INF-IAI-MCI/BrailleIO "A 2D tactile pin-matrix device abstraction framework")



## Configuration

You can configure the TCP/IP port of the MVBD connection by adding the configuration key
**__MVBD_TCPIP_Port_**
to your app.config file of the running application (_.exe_). The default value is 2018.

If such a file does not exist, you can simply create a new file named **_app.config_** beseid your 
application executable file (_*.exe_).

The content of the file have to look something like this:

```xml
<?xml version="1.0"?>
<configuration>
  <appSettings>

   <add key="MVBD_TCPIP_Port" value="2018" />

 </appSettings>
</configuration>
```
