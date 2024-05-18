using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using NativeWifi;
using System;
using System.Linq;
using System.Xml.Linq; 

namespace SecLinkApp
{
    public static class NetworkHelper
    {

        public static (string Name, string Type) GetConnectedNetworkDetails()
        {
            // Get currently connected networks
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface nic in interfaces)
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    string connectionType = nic.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                        NetworkInterfaceType.Ethernet => "Ethernet",
                        _ => "Other"
                    };

                    // Get network name
                    string networkName = connectionType == "Wi-Fi"
                                         ? GetWifiSSID(nic)
                                         : GetNetworkNameFromGateway(nic);

                    return (networkName, connectionType);
                }
            }

            return ("Not Connected", "None");
        }

        //get the Wi-Fi SSID using Native Wifi API 
        private static string GetWifiSSID(NetworkInterface nic)
        {
            try
            {
                var wlanClient = new WlanClient();
                foreach (var wlanInterface in wlanClient.Interfaces)
                {
                    if (wlanInterface.NetworkInterface.Id == nic.Id)
                    {
                        // getting profile XML as a string
                        string profileXml = wlanInterface.GetProfileXml(wlanInterface.CurrentConnection.profileName);
                        XDocument profileDoc = XDocument.Parse(profileXml);
                        XNamespace ns = "http://www.microsoft.com/networking/WLAN/profile/v1";

                        // Query the XML for the SSID name
                        var ssidName = profileDoc.Descendants(ns + "name").FirstOrDefault()?.Value;

                        if (!string.IsNullOrEmpty(ssidName))
                        {
                            return ssidName;
                        }
                    }
                }
            }
            catch (Exception)
            {
               
            }

            return "Unknown SSID";
        }

        // Helper for other connection types
        private static string GetNetworkNameFromGateway(NetworkInterface nic)
        {
            IPInterfaceProperties properties = nic.GetIPProperties();
            GatewayIPAddressInformationCollection addresses = properties.GatewayAddresses;
            foreach (GatewayIPAddressInformation address in addresses)
            {
                Console.WriteLine("getway address : " + address.ToString());
            }
            if (addresses.Count > 0)
            {
                return addresses[0].Address.ToString();
            }
            return "Unknown Network or Problem with the Network Adapters";
        }


    }
}