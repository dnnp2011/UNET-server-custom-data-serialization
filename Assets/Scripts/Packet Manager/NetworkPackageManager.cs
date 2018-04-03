using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;

public class NetworkPackageManager<T> where T : class {
    public event Action<byte[]> OnRequirePackageTransmit;

    private float m_sendSpeed = 0.2f;
    public float SendSpeed
    {
        get {
            if (m_sendSpeed < 0.1f)
                return m_sendSpeed = 0.1f;
            return m_sendSpeed;
        }
        set { m_sendSpeed = value; }
    }

    private float nextTick;

    private List<T> m_packages;
    public List<T> Packages
    {
        get {
            if (m_packages == null)
                m_packages = new List<T>();
            return m_packages;
        }
    }

    public Queue<T> ReceivedPackages;


    ///<summary>
    ///Add a package to the List<T> to be transmitted
    ///</summary>
    ///<param name="_package"></param>
    public void AddPackage(T _package)
    {
        Packages.Add(_package);
    }

    ///<summary>
    ///Deserialize received package, add to queue to be processed
    ///</summary>
    ///<param name="_bytes"></param>
    public void ReceiveData(byte[] _bytes)
    {
        if (ReceivedPackages == null)
            ReceivedPackages = new Queue<T>();

        T[] packages = ReadBytes(_bytes).ToArray();

        for (int i = 0; i < packages.Length; i++)
        {
            ReceivedPackages.Enqueue(packages[i]);
        }
    }


    ///<summary>
    ///Increment nextTick after every Unity fixedUpdate
    ///</summary>
    ///<param name="Tick"></param>
    public void Tick()
    {
        nextTick += 1 / this.SendSpeed * Time.fixedDeltaTime;
        if (nextTick > 1 && Packages.Count > 0)
        {
            nextTick = 0;
            if (OnRequirePackageTransmit != null)
            {
                byte[] bytes = CreateBytes();
                Packages.Clear();
                OnRequirePackageTransmit(bytes);
            }
        }
    }


    ///<summary>
    ///Returns the next received package in the queue
    ///</summary>
    ///<param name="GetNextDataReceived"></param>
    public T GetNextDataReceived()
    {
        if (ReceivedPackages == null || ReceivedPackages.Count == 0)
            return default(T);

        return ReceivedPackages.Dequeue();
    }


    ///<summary>
    ///Serializes a package to be sent over network
    ///</summary>
    ///<param name="CreateBytes"></param
    byte[] CreateBytes()
    {
        BinaryFormatter formatter = new BinaryFormatter();
        using (MemoryStream ms = new MemoryStream())
        {
            formatter.Serialize(ms, this.Packages);
            return ms.ToArray();
        }
    }


    ///<summary>
    ///Deserializes a package that has been sent over the network
    ///</summary>
    ///<param name="ReadBytes"></param>
    List<T> ReadBytes(byte[] bytes)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        using (MemoryStream ms = new MemoryStream())
        {
            ms.Write(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            return (List<T>)formatter.Deserialize(ms);
        }
    }


}