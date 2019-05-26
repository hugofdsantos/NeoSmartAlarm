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
        // public delegate bool AnotherContract(string name);
        private static readonly byte[] owner = "AcjwdaeMyuzxRUJd4LdqHNML627hKZpdhq".ToScriptHash();

        /*********
        args[0] => script hash reversed of the contract to be registered (scheduled)
        args[1] => how frequent the contract should run in seconds
        **********/
        public static bool Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(owner);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "register")
                {
                    return Register(args);
                }
                else if (operation == "execute")
                {
                    return Execute(args);
                }
            }

            Runtime.Notify("No operation found!");
            return false;
        }

        private static bool Execute(object[] args)
        {
            var scriptHash = (byte[])args[0];
            var serializedEntry = Storage.Get(Storage.CurrentContext, scriptHash);
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
            var contractToBeInvoked = (AnotherContract)scriptHash.ToDelegate();
            var result = contractToBeInvoked((string)contractParams[0], (object[])contractParams[1]);  //errado!
            // var result = contractToBeInvoked((string)contractParams[0]);
            if (!result)
            {
                Runtime.Notify("Error in contract execution.");
                return false;
            }

            entry[2] = timeStamp;
            Storage.Put(Storage.CurrentContext, scriptHash, entry.Serialize());
            return true;
        }

        private static bool Register(object[] args)
        {
            var scriptHash = (byte[])args[0];
            if (!isValidContract(scriptHash))
            {
                return false;
            }

            var frequency = (uint)args[1];
            if (frequency < 3600)
            {
                Runtime.Notify("The given frequency is invalid.");
                return false;
            }

            var paramsOfContract = parseParams(args);
            var newEntry = arrangeObjectToStore(paramsOfContract, frequency);
            Storage.Put(Storage.CurrentContext, scriptHash, newEntry.Serialize());

            return true;
        }

        private static bool isValidContract(byte[] scriptHash)
        {
            var contract = HelperExternal.GetContract(scriptHash);
            if (!contract)
            {
                Runtime.Notify("The given scripthash is invalid.");
                return false;
            }
            else if ((Storage.Get(Storage.CurrentContext, scriptHash)).Length != 0)
            {
                Runtime.Notify("Contract already registered.");
                return false;
            }
            return true;
        }

        private static object[] parseParams(object[] args)
        {
            var parameters = new Object[args.Length - 2];
            for (int i = 0; i < args.Length - 2; i++)
            {
                parameters[i] = args[i + 2];
            }
            return parameters;
        }

        private static object[] arrangeObjectToStore(object[] paramsToInvoke, uint frequency)
        {
            var entry = new Object[3];
            entry[0] = paramsToInvoke;
            entry[1] = frequency;
            entry[2] = 0; //last execution
            return entry;
        }

    }
}