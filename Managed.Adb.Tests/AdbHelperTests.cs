﻿using Managed.Adb.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Managed.Adb.Tests
{
    [TestClass]
    public class AdbHelperTests
    {
        [TestMethod]
        public void FormAdbRequestTest()
        {
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("0009host:kill\n"), AdbHelper.FormAdbRequest("host:kill"));
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("000Chost:version\n"), AdbHelper.FormAdbRequest("host:version"));
        }

        [TestMethod]
        public void CreateAdbForwardRequestTest()
        {
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("0008tcp:1984\n"), AdbHelper.CreateAdbForwardRequest(null, 1984));
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("0012tcp:1981:127.0.0.1\n"), AdbHelper.CreateAdbForwardRequest("127.0.0.1", 1981));
        }

        IAdbSocketFactory factory;
        IDummyAdbSocket socket;
        IPEndPoint endPoint;

        bool integrationTest = false;

        [TestInitialize]
        public void Initialize()
        {
            // Use the tracing adb socket factory to run the tests on an actual device.
            // use the dummy socket factory to run unit tests.
            if (integrationTest)
            {
                this.factory = new TracingAdbSocketFactory();
            }
            else
            {
                this.factory = new DummyAdbSocketFactory();
            }

            this.socket = (IDummyAdbSocket)factory.Create(AdbServer.SocketAddress);
            AdbHelper.SocketFactory = factory;
            this.endPoint = AdbServer.SocketAddress;
        }

        [TestMethod]
        public void KillAdbTest()
        {
            var responses = new AdbResponse[] { };

            var responseMessages = new string[] { };

            var requests = new string[]
            {
                "host:kill"
            };

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () =>
                {
                    AdbHelper.Instance.KillAdb(this.endPoint);
                });
        }

        [TestMethod]
        public void GetAdbVersionTest()
        {
            var responses = new AdbResponse[]
            {
                AdbResponse.OK
            };

            var responseMessages = new string[]
            {
                "0020"
            };

            var requests = new string[]
            {
                "host:version"
            };

            int version = 0;

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () =>
                {
                    version = AdbHelper.Instance.GetAdbVersion(endPoint);
                });

            // Make sure and the correct value is returned.
            Assert.AreEqual(32, version);
        }

        [TestMethod]
        public void GetDevicesTest()
        {
            var responses = new AdbResponse[]
            {
                AdbResponse.OK
            };

            var responseMessages = new string[]
            {
                "169.254.109.177:5555   device product:VS Emulator 5\" KitKat (4.4) XXHDPI Phone model:5__KitKat__4_4__XXHDPI_Phone device:donatello\n"
            };

            var requests = new string[]
            {
                "host:devices-l"
            };

            List<DeviceData> devices = null;

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () =>
                {
                    devices = AdbHelper.Instance.GetDevices(endPoint);
                });

            // Make sure and the correct value is returned.
            Assert.IsNotNull(devices);
            Assert.AreEqual(1, devices.Count);

            var device = devices.Single();
            
            Assert.AreEqual("169.254.109.177:5555", device.Serial);
            Assert.AreEqual(DeviceState.Online, device.State);
            Assert.AreEqual("5__KitKat__4_4__XXHDPI_Phone", device.Model);
            Assert.AreEqual("donatello", device.Name);
        }

        [TestMethod]
        public void SetDeviceTest()
        {
            var device = new DeviceData()
            {
                Serial = "169.254.109.177:5555",
                State = DeviceState.Online
            };

            var responses = new AdbResponse[]
            {
                AdbResponse.OK
            };

            var responseMessages = new string[] { };

            var requests = new string[]
            {
                "host:transport:169.254.109.177:5555"
            };

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () =>
                {
                    AdbHelper.Instance.SetDevice(this.socket, device);
                });
        }

        [TestMethod]
        public void ExecuteRemoteCommandTest()
        {
            var device = new DeviceData()
            {
                Serial = "169.254.109.177:5555",
                State = DeviceState.Online
            };

            var responses = new AdbResponse[]
            {
                AdbResponse.OK,
                AdbResponse.OK
            };

            var responseMessages = new string[] { };

            var requests = new string[]
            {
                "host:transport:169.254.109.177:5555",
                "shell:echo Hello, World"
            };

            var receiver = new ConsoleOutputReceiver();

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () =>
                {
                    AdbHelper.Instance.ExecuteRemoteCommand(this.endPoint, "echo Hello, World", device, receiver);
                });

            Assert.AreEqual("Hello, World\r\n", receiver.ToString());
        }

        [TestMethod]
        public void CreateForwardTest()
        {
            this.RunCreateForwardTest(
                (device) => AdbHelper.Instance.CreateForward(this.endPoint, device, "tcp:1", "tcp:2", true),
                "tcp:1;tcp:2");
        }

        [TestMethod]
        public void CreateTcpForwardTest()
        {
            this.RunCreateForwardTest(
                (device) => AdbHelper.Instance.CreateForward(this.endPoint, device, 3, 4),
                "tcp:3;tcp:4");
        }

        [TestMethod]
        public void CreateSocketForwardTest()
        {
            this.RunCreateForwardTest(
                (device) => AdbHelper.Instance.CreateForward(this.endPoint, device, 5, "/socket/1"),
                "tcp:5;local:/socket/1");
        }

        [TestMethod]
        [ExpectedException(typeof(AdbException))]
        public void CreateDuplicateForwardTest()
        {
            var device = new DeviceData()
            {
                Serial = "169.254.109.177:5555",
                State = DeviceState.Online
            };

            var responses = new AdbResponse[]
            {
                AdbResponse.FromError("cannot rebind existing socket")
            };

            var responseMessages = new string[] { };

            var requests = new string[]
            {
                "host-serial:169.254.109.177:5555:forward:norebind:tcp:1;tcp:2"
            };

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () =>
                {
                    AdbHelper.Instance.CreateForward(this.endPoint, device, "tcp:1", "tcp:2", false);
                });
        }

        [TestMethod]
        public void ListForwardTest()
        {
            var device = new DeviceData()
            {
                Serial = "169.254.109.177:5555",
                State = DeviceState.Online
            };

            var responses = new AdbResponse[]
            {
                AdbResponse.OK
            };

            var responseMessages = new string[] {
                "169.254.109.177:5555 tcp:1 tcp:2\n169.254.109.177:5555 tcp:3 tcp:4\n169.254.109.177:5555 tcp:5 local:/socket/1\n"
            };

            var requests = new string[]
            {
                "host-serial:169.254.109.177:5555:list-forward"
            };

            ForwardData[] forwards = null;

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () => forwards = AdbHelper.Instance.ListForward(this.endPoint, device).ToArray());

            Assert.IsNotNull(forwards);
            Assert.AreEqual(3, forwards.Length);
            Assert.AreEqual("169.254.109.177:5555", forwards[0].SerialNumber);
            Assert.AreEqual("tcp:1", forwards[0].Local);
            Assert.AreEqual("tcp:2", forwards[0].Remote);
        }

        [TestMethod]
        public void RemoveForwardTest()
        {
            var device = new DeviceData()
            {
                Serial = "169.254.109.177:5555",
                State = DeviceState.Online
            };

            var responses = new AdbResponse[]
            {
                AdbResponse.OK
            };

            var responseMessages = new string[] { };

            var requests = new string[]
            {
                "host-serial:169.254.109.177:5555:killforward:tcp:1"
            };

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () => AdbHelper.Instance.RemoveForward(this.endPoint, device, 1));
        }

        [TestMethod]
        public void RemoveAllForwardsTest()
        {
            var device = new DeviceData()
            {
                Serial = "169.254.109.177:5555",
                State = DeviceState.Online
            };

            var responses = new AdbResponse[]
            {
                AdbResponse.OK
            };

            var responseMessages = new string[] { };

            var requests = new string[]
            {
                "host-serial:169.254.109.177:5555:killforward-all"
            };

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () => AdbHelper.Instance.RemoveAllForwards(this.endPoint, device));
        }

        [TestMethod]
        public void ConnectIPAddressTest()
        {
            this.RunConnectTest(
                () => AdbHelper.Instance.Connect(this.endPoint, IPAddress.Loopback),
                "127.0.0.1:5555");
        }

        [TestMethod]
        public void ConnectDnsEndpointTest()
        {
            this.RunConnectTest(
                () => AdbHelper.Instance.Connect(this.endPoint, new DnsEndPoint("localhost", 1234)),
                "localhost:1234");
        }

        [TestMethod]
        public void ConnectIPEndpointTest()
        {
            this.RunConnectTest(
                () => AdbHelper.Instance.Connect(this.endPoint, new IPEndPoint(IPAddress.Loopback, 4321)),
                "127.0.0.1:4321");
        }

        [TestMethod]
        public void ConnectHostEndpointTest()
        {
            this.RunConnectTest(
                () => AdbHelper.Instance.Connect(this.endPoint, "localhost"),
                "localhost:5555");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConnectIPAddressNullTest()
        {
            AdbHelper.Instance.Connect(this.endPoint, (IPAddress)null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConnectDnsEndpointNullTest()
        {
            AdbHelper.Instance.Connect(this.endPoint, (DnsEndPoint)null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConnectIPEndpointNullTest()
        {
            AdbHelper.Instance.Connect(this.endPoint, (IPEndPoint)null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConnectHostEndpointNullTest()
        {
            AdbHelper.Instance.Connect(this.endPoint, (string)null);
        }

        public void RunConnectTest(Action test, string connectString)
        {
            var responses = new AdbResponse[]
            {
                AdbResponse.OK
            };

            var responseMessages = new string[] { };

            var requests = new string[]
            {
                $"host:connect:{connectString}"
            };

            this.RunTest(
                responses,
                responseMessages,
                requests,
                test);
        }

        private void RunCreateForwardTest(Action<DeviceData> test, string forwardString)
        {
            var device = new DeviceData()
            {
                Serial = "169.254.109.177:5555",
                State = DeviceState.Online
            };

            var responses = new AdbResponse[]
            {
                AdbResponse.OK
            };

            var responseMessages = new string[] { };

            var requests = new string[]
            {
                $"host-serial:169.254.109.177:5555:forward:{forwardString}"
            };

            this.RunTest(
                responses,
                responseMessages,
                requests,
                () => test(device));
        }

        /// <summary>
        /// <para>
        /// Runs an ADB helper test, either as a unit test or as an integration test.
        /// </para>
        /// <para>
        /// When running as a unit test, the <paramref name="responses"/> and <paramref name="responseMessages"/>
        /// are used by the <see cref="DummyAdbSocket"/> to mock the responses an actual device
        /// would send; and the <paramref name="requests"/> parameter is used to ensure the code
        /// did send the correct requests to the device.
        /// </para>
        /// <para>
        /// When running as an integration test, all three parameters, <paramref name="responses"/>,
        /// <paramref name="responseMessages"/> and <paramref name="requests"/> are used to validate
        /// that the traffic we simulate in the unit tests matches the trafic that is actually sent
        /// over the wire.
        /// </para>
        /// </summary>
        /// <param name="responses">
        /// The <see cref="AdbResponse"/> messages that the ADB sever should send.
        /// </param>
        /// <param name="responseMessages">
        /// The messages that should follow the <paramref name="responses"/>.
        /// </param>
        /// <param name="requests">
        /// The requests the client should send.
        /// </param>
        /// <param name="test">
        /// The test to run.
        /// </param>
        private void RunTest(
            IEnumerable<AdbResponse> responses,
            IEnumerable<string> responseMessages,
            IEnumerable<string> requests,
            Action test)
        {
            // If we are running unit tests, we need to mock all the responses
            // that are sent by the device. Do that now.
            if (!integrationTest)
            {
                foreach (var response in responses)
                {
                    socket.Responses.Enqueue(response);
                }

                foreach (var responseMessage in responseMessages)
                {
                    socket.ResponseMessages.Enqueue(responseMessage);
                }
            }

            Exception exception = null;

            try
            {
                test();
            }
            catch(Exception ex)
            {
                exception = ex;
            }

            if (!integrationTest)
            {
                // If we are running unit tests, we need to make sure all messages
                // were read, and the correct request was sent.

                // Make sure the messages were read
                Assert.AreEqual(0, socket.ResponseMessages.Count);
                Assert.AreEqual(0, socket.Responses.Count);

                // Make sure a request was sent
                CollectionAssert.AreEqual(requests.ToList(), socket.Requests);
            }
            else
            {
                // Make sure the traffic sent on the wire matches the traffic
                // we have defined in our unit test.
                CollectionAssert.AreEqual(requests.ToList(), socket.Requests);
                CollectionAssert.AreEqual(responses.ToList(), socket.Responses);
                CollectionAssert.AreEqual(responseMessages.ToList(), socket.ResponseMessages);
            }

            if(exception != null)
            {
                throw exception;
            }
        }
    }
}
