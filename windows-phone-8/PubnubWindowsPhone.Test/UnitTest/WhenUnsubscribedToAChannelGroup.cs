﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Phone.Testing;
using PubNubMessaging.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace PubnubWindowsPhone.Test.UnitTest
{
    [TestClass]
    public class WhenUnsubscribedToAChannelGroup : WorkItemTest
    {
        ManualResetEvent unsubscribeManualEvent = new ManualResetEvent(false);
        ManualResetEvent grantManualEvent = new ManualResetEvent(false);

        bool receivedMessage = false;
        bool receivedGrantMessage = false;
        bool receivedChannelGroupMessage = false;
        bool receivedChannelGroupConnectedMessage = false;

        string currentUnitTestCase = "";
        string channelGroupName = "hello_my_group";

        int manualResetEventsWaitTimeout = 310 * 1000;

        [ClassInitialize, Asynchronous]
        public void Init()
        {
            if (!PubnubCommon.PAMEnabled) return;

            receivedGrantMessage = false;

            ThreadPool.QueueUserWorkItem((s) =>
                {
                    Pubnub pubnub = new Pubnub(PubnubCommon.PublishKey, PubnubCommon.SubscribeKey, PubnubCommon.SecretKey, "", false);

                    PubnubUnitTest unitTest = new PubnubUnitTest();
                    unitTest.TestClassName = "GrantRequestUnitTest";
                    unitTest.TestCaseName = "Init3";
                    pubnub.PubnubUnitTest = unitTest;

                    pubnub.ChannelGroupGrantAccess<string>(channelGroupName, true, true, 20, ThenChannelGroupInitializeShouldReturnGrantMessage, DummyUnsubscribeErrorCallback);
                    Thread.Sleep(1000);

                    grantManualEvent.WaitOne();

                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            Assert.IsTrue(receivedGrantMessage, "WhenUnsubscribedToAChannelGroup Grant access failed.");
                            pubnub.PubnubUnitTest = null;
                            pubnub = null;
                            TestComplete();
                        });
                });
        }

        [TestMethod, Asynchronous]
        public void ThenShouldReturnUnsubscribedMessage()
        {
            currentUnitTestCase = "ThenShouldReturnUnsubscribedMessage";
            receivedMessage = false;
            receivedChannelGroupMessage = false;
            receivedChannelGroupConnectedMessage = false;

            ThreadPool.QueueUserWorkItem((s) =>
                {
                    Pubnub pubnub = new Pubnub(PubnubCommon.PublishKey, PubnubCommon.SubscribeKey, "", "", false);
                    pubnub.SessionUUID = "myuuid";

                    PubnubUnitTest unitTest = new PubnubUnitTest();
                    unitTest.TestClassName = "WhenUnsubscribedToAChannelGroup";
                    unitTest.TestCaseName = "ThenShouldReturnUnsubscribedMessage";

                    pubnub.PubnubUnitTest = unitTest;

                    channelGroupName = "hello_my_group";
                    string channelName = "hello_my_channel";

                    unsubscribeManualEvent = new ManualResetEvent(false);
                    pubnub.AddChannelsToChannelGroup<string>(new string[] { channelName }, channelGroupName, ChannelGroupAddCallback, DummySubscribeErrorCallback);
                    unsubscribeManualEvent.WaitOne(manualResetEventsWaitTimeout);
                    if (receivedChannelGroupMessage)
                    {
                        unsubscribeManualEvent = new ManualResetEvent(false);
                        pubnub.Subscribe<string>("", channelGroupName, DummyMethodChannelSubscribeUserCallback, DummyMethodChannelSubscribeConnectCallback, DummyErrorCallback);
                        Thread.Sleep(1000);
                        unsubscribeManualEvent.WaitOne(manualResetEventsWaitTimeout);

                        if (receivedChannelGroupConnectedMessage)
                        {
                            unsubscribeManualEvent = new ManualResetEvent(false);
                            pubnub.Unsubscribe<string>("", channelGroupName, DummyMethodUnsubscribeChannelUserCallback, DummyMethodUnsubscribeChannelConnectCallback, DummyMethodUnsubscribeChannelDisconnectCallback, DummyErrorCallback);
                            unsubscribeManualEvent.WaitOne(manualResetEventsWaitTimeout);
                        }

                        pubnub.EndPendingRequests();

                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                            {
                                Assert.IsTrue(receivedMessage, "WhenUnsubscribedToAChannelGroup --> ThenShouldReturnUnsubscribedMessage Failed");
                                pubnub.PubnubUnitTest = null;
                                pubnub = null;
                                TestComplete();
                            });
                    }
                    else
                    {
                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                            {
                                Assert.IsTrue(receivedChannelGroupMessage, "WhenUnsubscribedToAChannelGroup --> ThenShouldReturnUnsubscribedMessage Failed");
                                pubnub.PubnubUnitTest = null;
                                pubnub = null;
                                TestComplete();
                            });
                    }
                });
        }

        [Asynchronous]
        private void DummyMethodChannelSubscribeUserCallback(string result)
        {
        }

        [Asynchronous]
        private void DummyMethodChannelSubscribeConnectCallback(string result)
        {
            if (result.Contains("Connected"))
            {
                receivedChannelGroupConnectedMessage = true;
            }
            unsubscribeManualEvent.Set();
        }

        [Asynchronous]
        private void DummyMethodUnsubscribeChannelUserCallback(string result)
        {
        }

        [Asynchronous]
        private void DummyMethodUnsubscribeChannelConnectCallback(string result)
        {
        }

        [Asynchronous]
        private void DummyMethodUnsubscribeChannelDisconnectCallback(string result)
        {
            if (result.Contains("Unsubscribed from"))
            {
                receivedMessage = true;
            }
            unsubscribeManualEvent.Set();
        }

        [Asynchronous]
        void ChannelGroupAddCallback(string receivedMessage)
        {
            try
            {
                if (!string.IsNullOrEmpty(receivedMessage) && !string.IsNullOrEmpty(receivedMessage.Trim()))
                {
                    object[] serializedMessage = JsonConvert.DeserializeObject<object[]>(receivedMessage);
                    JContainer dictionary = serializedMessage[0] as JContainer;
                    if (dictionary != null)
                    {
                        int statusCode = dictionary.Value<int>("status");
                        string serviceType = dictionary.Value<string>("service");
                        bool errorStatus = dictionary.Value<bool>("error");
                        string currentChannelGroup = serializedMessage[1].ToString().Substring(1); //assuming no namespace for channel group
                        string statusMessage = dictionary.Value<string>("message");
                        if (statusCode == 200 && statusMessage.ToLower() == "ok" && serviceType == "channel-registry" && !errorStatus)
                        {
                            if (currentChannelGroup == channelGroupName)
                            {
                                receivedChannelGroupMessage = true;
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                unsubscribeManualEvent.Set();
            }

        }

        [Asynchronous]
        void ThenChannelGroupInitializeShouldReturnGrantMessage(string receivedMessage)
        {
            try
            {
                if (!string.IsNullOrEmpty(receivedMessage) && !string.IsNullOrEmpty(receivedMessage.Trim()))
                {
                    object[] serializedMessage = JsonConvert.DeserializeObject<object[]>(receivedMessage);
                    JContainer dictionary = serializedMessage[0] as JContainer;
                    var status = dictionary["status"].ToString();
                    if (status == "200")
                    {
                        receivedGrantMessage = true;
                    }
                }
            }
            catch { }
            finally
            {
                grantManualEvent.Set();
            }
        }

        [Asynchronous]
        private void DummyUnsubscribeErrorCallback(PubnubClientError result)
        {
            unsubscribeManualEvent.Set();
        }

        [Asynchronous]
        private void DummySubscribeErrorCallback(PubnubClientError result)
        {
            unsubscribeManualEvent.Set();
        }

        [Asynchronous]
        private void DummyErrorCallback(PubnubClientError result)
        {
        }

    }
}
