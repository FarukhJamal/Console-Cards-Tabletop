Datagram Encryption
for the C# library



Requirements

You will need a recent Photon dll, DatagramEncryptorNative.cs and a "PhotonDatagramEncryptorPlugin" native library (per platform).
All of this is provided by the Photonians on demand.

We suggest to put everything in the folder:
    Photon\PhotonLibs\PhotonDatagramEncryptorPlugin\

Clients must initially connect with WSS (more below).



Project Files Setup

The actual PhotonDatagramEncryptorPlugin libraries come in a folder per platform with several sub-folders.
We include debug and release builds in most cases. Using the debug build is fine but some platforms may require release builds.

Use the inspector to make sure each library has the correct export setup.
We tried to define good default export settings but in some cases no library may export and in other cases, multiple. Both results in compile time errors.


Project Config

Fusion Datagram Encryption is described here:
https://doc.photonengine.com/fusion/current/manual/advanced/encryption


In the PhotonServerSettings configure:
	Protocol = UDP
	AuthMode = AuthOnceWss
	Port = 0	(or set 443 as this defines only the initial connection's port)

With AuthOnceWss, the initial connection to the Name Server will be via WSS port 443, then the selected Protocol is used. UDP must be selected for Datagram Encryption.


In your code, find the LoadBalancingClient you want encrypted.
In PUN 2 this is PhotonNetwork.NetworkingClient.

Add this line to make the DatagramEncryptorNative class available:
	using ExitGames.Client.Photon.Encryption;

With the client, set this before you connect:
    client.AuthMode = AuthModeOption.AuthOnceWss;	                    // this should also be set in the PhotonServerSettings
    client.EncryptionMode = EncryptionMode.DatagramEncryptionGCM;       // in the Realtime SDK v5, this is the only option and default
    client.LoadBalancingPeer.EncryptorType = typeof(DatagramEncryptorNative);


If you use ConnectUsingSettings(), the AuthMode can often be defined in the PhotonServerSettings.
The AuthMode is hidden in some versions of the PhotonServerSettings Inspector. In doubt, the "debug mode" for the inspector helps.
Alternatively set PhotonServerSettings.AuthMode by code before you call ConnectUsingSettings().



Confirmation

There are three initial datagrams, which are not encrypted.
These contain a Connect/VerifyConnect commands and the client sends a token, which it got from the Name Server.
This token is encrypted by the Photon Name Server and unreadable for the client.
The fourth datagram should be from the server and the first to be encrypted.

In encrypted datagrams, the third byte is always 1.
In wireshark you can no longer see text (e.g. when you send strings as part of RaiseEvent / events).



Troubleshooting

Keep in mind that Unity Editors never unload a native lib once it's loaded. You won't be able to overwrite it, etc.
Close Unity, move or replace the files, open Unity again.

If the connection fails, check the logs and look out for the ports being correct for WSS (see above).