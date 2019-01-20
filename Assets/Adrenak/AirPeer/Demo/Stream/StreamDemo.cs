using Byn.Net;
using UnityEngine;
using Adrenak.AirPeer;

public class StreamDemo : MonoBehaviour {
    Node host;
    Node client;

	void Start () {
        Application.runInBackground = true;

		host = Node.New();
        if (!host.Init()) {
            Debug.Log("Could not start network");
            return;
        }

		host.OnLeave += (cid) => {
			Debug.Log(cid.id);
		};

		host.OnGetPacket += delegate (ConnectionId arg1, Packet arg2, bool arg3) {
            Debug.Log("Host received message from " + arg1.id + " : " + arg2.Payload.ToUTF8String());
        };
        
        host.StartServer("room-name", success => {
            Debug.Log("Server started? : " + success);

            if (!success) return;
            StartClient(); 
        });
	}

    void StartClient() {
        client = Node.New();
        client.Init();

		client.OnLeave += (cid) => {
			Debug.Log(cid.id);
		};

        client.Connect("room-name", cid => {
            Debug.Log("Client connect success? : " + cid.IsValid());
        });
    }
	
	private void Update() {
        if (client != null && client.NodeState == Node.State.Client) {
            var msg = "Client says : " + Time.frameCount;
            client.Send(Packet.From(client).With("string", msg));            
        }

		if (Input.GetKeyDown(KeyCode.Escape))
			client.Disconnect();
    }
}
