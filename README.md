# Introduction
The configuration import / export utility is designed to enable users to make common configuration changes to their XProtect Advanced VMS system in bulk. Common usage scenarios include...

Modify the username and/or password used to authenticate with hardware devices. This is useful if your camera vendor provides a tool to change passwords for your cameras in bulk. With the config import/export utility, you can very quickly update the password used by Milestone to match the new password(s) on your devices.
Modify the address used to reach your hardware devices. If you have changed the IP range or hostname scheme used for your surveillance network, you don't have to update each device one at a time.
Modify the display name of your hardware and/or related devices in bulk.
Disable additional camera channels in bulk. This is helpful if you have many cameras which have up to 8 or more channels, but you only use the first channel.
Document your environment by exporting the information about your hardware, cameras, microphones, speakers, inputs, outputs and metadata in a functional CSV format.
Automatically add cameras to an XProtect Advanced VMS product and rename them based on an XProtect Professional VMS product version.
Usage
Running configimportexportutil.exe with the --help flag produces the following...
 
```
ConfigImportExportUtil 1.0.6530.23239
Copyright c  2017

  -i, --import      Import data from CSV.

  -e, --export      Export data to CSV.

  -m, --migrate     Export from a local Professional VMS installation to an
                    Advanced VMS system. Use with -k hardware

  -u, --update      Update a specific property like hardware password. Use with
                    --kind hardware --property password

  -r, --recorder    Name of the Recording Server to which cameras will be
                    migrated from a local Professional VMS installation. Use
                    with -m -k hardware -g groupName

  -g, --group       Name of the Camera Group to which cameras will be migrated
                    from a local Professional VMS installation. Use with -m -k
                    hardware

  -k, --kind        Required. Kind of data to import/export. Options: recorder,
                    hardware, camera, microphone, speaker, metadata, input,
                    output

  -o, --property    Use with --update. Current supported use is --kind hardware
                    --property password --value newpassword

  -a, --value       Use with --update. Current supported use is --kind hardware
                    --property password --value newpassword

  -n, --new         Import new hardware from CSV or export a CSV template with
                    the -e flag. Note that only the first camera channel will
                    be enabled. All other channels and attached devices will be
                    disabled by default.

  -s, --server      (Default: localhost) Milestone XProtect Management Server
                    hostname or IP

  -p, --port        (Default: 80) HTTP port used by Milestone XProtect
                    Management Server. Default: 80.

  -f, --file        Filename of source or destination CSV file.

  -v, --verbose     Log verbose info to console

  --help            Display this help screen.

  --version         Display version information.
```  

# Examples
Update the password for all hardware configured in the VMS in one command (note: this does not change the password on the device itself, only the password used by the VMS):
```
configimportexportutil.exe -u -k hardware --property password --value newS3cretPassw0rd
```

Export information about all hardware to a CSV file named hardware.csv from a server named "server05.surveillance.local":
```
configimportexportutil.exe -s server05.surveillance.local -k hardware -e -f hardware.csv
```

Import modifed hardware settings (modified name, address, username, password and/or enabled status) from the previously exported hardware.csv:
``` 
configimportexportutil.exe -s server05.surveillance.local -k hardware -i -f hardware.csv
```

Migrate all hardware/camera names and enabled/disabled status from a local installation of XProtect Professional to a remote Advanced VMS product:
``` 
configimportexportutil.exe -s server05.surveillance.local -k hardware -m -r RecordingServer1 -g CameraGroup1
```
_Note: hardware will be added to Recording Server named RecordingServer1, and the attached cameras will be added to CameraGroup1 and if this camera group doesn't exist, it will be created. Other devices like speakers, microphones, inputs, outputs and metadata will all be disabled by default and will retain default names._

Export information about all microphones from a local installation of an Advanced VMS product to a CSV file named mics.csv:
``` 
configimportexportutil.exe -k hardware -e -f hardware.csv
```
