using System;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace Neo.SmartContract
{
    public static class HelperExternal
    {
        [Syscall("Neo.Blockchain.GetContract")]
        public static extern bool GetContract(byte[] scriptHash);
    }
    public class SmartAlarm : Framework.SmartContract
    {
        public delegate bool AnotherContract(string method, object[] args);
        private static readonly byte[] owner = "AcjwdaeMyuzxRUJd4LdqHNML627hKZpdhq".ToScriptHash();
        private static readonly byte[] test = "7b3984fec46a1410cccc56da5d52536fd1bf2330".HexToBytes();

        public static bool Main(string operation, object[] args, params object[] paramsToInvoke)
        {
            /*
            integers are always multiplied by 10^3 to simulate float numbers
            args[0] => script hash of the contract to be registered reversed (scheduled)
            args[1] => how frequent the contract should run
             */

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(owner);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "register") return Register(args, paramsToInvoke);
                else if (operation == "execute") return Execute(args, paramsToInvoke);
            }
            Runtime.Notify("No operation found!");

            return false;
        }

        static bool Execute(object[] args, object[] paramsToInvoke)
        {
            var contract = Blockchain.GetContract((byte[])args[0]);
            var serializedEntry = Storage.Get(Storage.CurrentContext, contract.Script);
            if (serializedEntry.Length == 0)
            {
                Runtime.Notify("The given contract isnt registered");
                return false;
            }
            var entry = (object[])serializedEntry.Deserialize();

            var frequency = (uint)entry[1];
            var lastExecution = (uint)entry[2];
            var timeStamp = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;
            if (timeStamp - lastExecution <= frequency)
            {
                Runtime.Notify("Its not time for contract execution.");
                return false;
            }

            //contract execution
            var contractParams = (object[])entry[0];
            var contractToBeInvoked = (AnotherContract)contract.Script.ToDelegate();
            var result = (bool)contractToBeInvoked((string)contractParams[0], (object[])contractParams[1]);
            if (!result)
            {
                Runtime.Notify("Error in contract execution.");
                return false;
            }

            entry[2] = timeStamp;
            Storage.Put(Storage.CurrentContext, contract.Script, entry.Serialize());
            return true;
        }

        static bool Register(object[] args, object[] paramsToInvoke)
        {
            object[] newEntry = new object[3];

            // var scriptHashReversed = (byte[])args[0];        
            var scriptHashReversed = test;
            var contract = HelperExternal.GetContract(scriptHashReversed);
            if (!contract)
            {
                Runtime.Notify("The given scripthash is invalid.");
                return false;
            }
            else if ((Storage.Get(Storage.CurrentContext, scriptHashReversed)).Length != 0)
            {
                Runtime.Notify("Contract already registered.");
                return false;
            }

            var frequency = (uint)args[1];
            if (frequency <= 2000 || frequency > 8760000)
            {
                Runtime.Notify("The given frequency is invalid.");
                return false;
            }

            // var contractParams = paramsToInvoke;
            newEntry[0] = paramsToInvoke;
            newEntry[1] = frequency;
            newEntry[2] = 0; //last execution
            Storage.Put(Storage.CurrentContext, scriptHashReversed, newEntry.Serialize());
            
            return true;
        }
    }
}