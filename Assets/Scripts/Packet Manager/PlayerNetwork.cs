using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(CharacterController))]
public class PlayerNetwork : NetworkPacketController {
    [SerializeField]
    float moveSpeed;

    [SerializeField]
    [Range(0.1f, 1f)]
    float networkSendRate = 0.5f;

    [SerializeField]
    bool isPredictionEnabled = true;

    [SerializeField]
    [Range(0.1f, 2f)]
    float correctionThreshold = 0.5f;

    CharacterController controller;

    List<ReceivePackage> predictedPackages;

    Vector3 lastPosition;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        PackageManager.SendSpeed = networkSendRate;
        ServerPackageManager.SendSpeed = networkSendRate;
        predictedPackages = new List<ReceivePackage>();
    }

    void Move(float horizontal, float vertical)
    {
        controller.Move(new Vector3(horizontal, vertical));
    }

    void Update()
    {
        LocalClientUpdate();
        ServerUpdate();
        RemoteClientUpdate();
    }

    void LocalClientUpdate()
    {
        if (!isLocalPlayer)
            return;
        if((Input.GetAxis("Horizontal") * moveSpeed) != 0 || (Input.GetAxis("Vertical") * moveSpeed) != 0)
        {
            float timeStep = Time.time;
            PackageManager.AddPackage(new Package
            {
                Horizontal = Input.GetAxis("Horizontal"),
                Vertical = Input.GetAxis("Vertical"),
                Timestamp = timeStep
            });

            if (isPredictionEnabled)
            {
                Move((Input.GetAxis("Horizontal") * moveSpeed), (Input.GetAxis("Vertical") * moveSpeed));
                predictedPackages.Add(new ReceivePackage
                {
                    Timestamp = timeStep,
                    X = transform.position.x,
                    Y = transform.position.y,
                    Z = transform.position.z
                });
            }
        }
    }

    void ServerUpdate()
    {
        if (!isServer || isLocalPlayer) //Precludes any player from being a server host
            return;

        Package packageData = PackageManager.GetNextDataReceived();

        if (packageData == null)
            return;

        Move(packageData.Horizontal * moveSpeed, packageData.Vertical * moveSpeed);

        if (transform.position == lastPosition)
            return;

        lastPosition = transform.position;

        ServerPackageManager.AddPackage(new ReceivePackage
        {
            X = transform.position.x,
            Y = transform.position.y,
            Z = transform.position.z,
            Timestamp = packageData.Timestamp

        });
    }

    public void RemoteClientUpdate()
    {
        if (isServer)
            return;

        var data = ServerPackageManager.GetNextDataReceived();

        if (data == null)
            return;

        if (isLocalPlayer && isPredictionEnabled)
        {
            var transmittedPackage = predictedPackages.Where(x => x.Timestamp == data.Timestamp).FirstOrDefault();
            if (transmittedPackage == null)
                return;

            //If the distance between where the server says player should be, and player says they are, is too high
            if (Vector3.Distance(new Vector3(transmittedPackage.X, transmittedPackage.Y, transmittedPackage.Z), new Vector3(data.X, data.Y, data.Z)) > correctionThreshold)
            {
                transform.position = new Vector3(data.X, data.Y, data.Z);
            }

            //Remove old packages
            predictedPackages.RemoveAll(x => x.Timestamp <= data.Timestamp);
        }
        else
        {
            transform.position = new Vector3(data.X, data.Y, data.Z);
        }
    }

}