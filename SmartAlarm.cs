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
        public delegate bool AnotherContract(object[] args);
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

            var serializedEntry = getContractFromStorageOrNull(scriptHash);
            if (serializedEntry == null)
            {
                Runtime.Notify("The given contract isnt registered");
                return false;
            }
            else if (!isContractDeployed(scriptHash))
            {
                Runtime.Notify("[execute] The given contract is not deployed on network");
                return false;
            }

            var entry = (object[])serializedEntry.Deserialize();
            var frequency = (uint)entry[1];
            var lastExecution = (uint)entry[2];

            var timeStamp = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;
            if (!isExecutionTime(timeStamp, lastExecution, frequency))
            {
                Runtime.Notify("Its not time for contract execution.");
                return false;                
            }

            var contractArgs = (object[])entry[0];
            var result = executeContract(scriptHash, contractArgs);
            if (!result)
            {
                Runtime.Notify("Error in contract execution.");
                return false;
            }
            Runtime.Notify("Contract invoked succesfully");

            entry[2] = timeStamp;
            Storage.Put(Storage.CurrentContext, scriptHash, entry.Serialize());
            return true;
        }

        private static bool executeContract(byte[] scriptHash, object[] args)
        {
            var contractInvoke = (AnotherContract)scriptHash.ToDelegate();
            return contractInvoke(args);
        }
        
        private static bool isExecutionTime(uint timeStamp, uint lastExecution, uint frequency)
        {
            if (timeStamp - lastExecution <= frequency)
            {
                return false;
            }
            return true;
        }

        private static bool Register(object[] args)
        {
            var scriptHash = (byte[])args[0];
            if (!isContractDeployed(scriptHash))
            {
                Runtime.Notify("[register] The given contract is not deployed on network");
                return false;
            }
            else if (getContractFromStorageOrNull(scriptHash) != null)
            {
                Runtime.Notify("Contract already registered.");
                return false;
            }

            var frequency = (uint)args[1];
            if (!isValidFrequency(frequency))
            {
                Runtime.Notify("The given frequency is invalid.");
                return false;
            }

            var paramsOfContract = parseParams(args);
            var newEntry = new Object[] {paramsOfContract, frequency, 0};
            Storage.Put(Storage.CurrentContext, scriptHash, newEntry.Serialize());

            return true;
        }

        private static bool isContractDeployed(byte[] scriptHash)
        {
            var contract = HelperExternal.GetContract(scriptHash);
            if (!contract)
            {
                return false;
            }
            return true;
        }

        private static byte[] getContractFromStorageOrNull(byte[] scriptHash)
        {
            var entry = Storage.Get(Storage.CurrentContext, scriptHash);
            if (entry.Length == 0)
            {
                return null;
            }
            return entry;
        }

        private static bool isValidFrequency(uint frequency)
        {
            if (frequency < 3600)
            {
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

    }
}