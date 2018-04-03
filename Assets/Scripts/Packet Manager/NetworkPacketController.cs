using UnityEngine;
using UnityEngine.Networking;

public class NetworkPacketController : NetworkBehaviour {


    ///<summary>
    ///A serializable object that can be sent over network
    ///</summary>
    ///<param name="Package"></param>
    [System.Serializable]
    public class Package
    {
        public float Horizontal;
        public float Vertical;
        public float Timestamp;
    }

    ///<summary>
    ///A serializable object that can be sent over network
    ///</summary>
    ///<param name="ReceivePackage"></param>
    [System.Serializable]
    public class ReceivePackage
    {
        public float X;
        public float Y;
        public float Z;
        public float Timestamp;
    }

    private NetworkPackageManager<Package> m_PackageManager;
    public NetworkPackageManager<Package> PackageManager
    {
        get {
            if (m_PackageManager == null)
            {
                m_PackageManager = new NetworkPackageManager<Package>();
                if (isLocalPlayer)
                    m_PackageManager.OnRequirePackageTransmit += TransmitPackageToServer;
            }
            return m_PackageManager;
        }
    }

    private NetworkPackageManager<ReceivePackage> m_ServerPackageManager;
    public NetworkPackageManager<ReceivePackage> ServerPackageManager
    {
        get {
            if (m_ServerPackageManager == null)
            {
                m_ServerPackageManager = new NetworkPackageManager<ReceivePackage>();
                if (isServer)
                    m_ServerPackageManager.OnRequirePackageTransmit += TransmitPackageToClients;
            }
            return m_ServerPackageManager;
        }
    }

    private void TransmitPackageToClients(byte[] bytes)
    {
        CmdTransmitPackages(bytes);
    }

    private void TransmitPackageToServer(byte[] bytes)
    {
        RpcReceiveDataOnClient(bytes);
    }

    [Command]
    void CmdTransmitPackages(byte[] data)
    {
        PackageManager.ReceiveData(data);
    }

    [ClientRpc]
    void RpcReceiveDataOnClient(byte[] data)
    {
        ServerPackageManager.ReceiveData(data);
    }

    public virtual void FixedUpdate()
    {
        PackageManager.Tick();
        ServerPackageManager.Tick();
    }
}