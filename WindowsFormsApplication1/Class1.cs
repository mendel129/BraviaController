using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApplication1
{
    public class SonyBraviaClass
    {
        public string ipadress { get; set; }
        public List<IndiSonyCommands> commands { get; set; }
        public string macadres { get; set; }
        public string cookie { get; set; }
    }
    
    class Class1
    {
    }
    public class SonyCommands
    {
        public int id { get; set; }
        public List<object> result { get; set; }
    }
    public class IndiSonyCommands
    {
        public string name { get; set; }
        public string value { get; set; }
    }
    public class IndiSonyOption
    {
        public string option { get; set; }
        public string value { get; set; }
    }
    public class IndiCookie
    {
        public string Comment { get; set; }
        public object CommentUri { get; set; }
        public bool HttpOnly { get; set; }
        public bool Discard { get; set; }
        public string Domain { get; set; }
        public bool Expired { get; set; }
        public string Expires { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Port { get; set; }
        public bool Secure { get; set; }
        public string TimeStamp { get; set; }
        public string Value { get; set; }
        public int Version { get; set; }
    }

}
