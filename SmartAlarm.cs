using System;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace Neo.SmartContract
{
    public class SmartAlarm : Framework.SmartContract
    {
        public delegate bool AnotherContract(string method, object[] args);
        private static readonly byte[] owner = "AcjwdaeMyuzxRUJd4LdqHNML627hKZpdhq".ToScriptHash();
        public static bool Main(string operation, params object[] args)
        {
            /*
            integers are always multiplied by 10^3 to simulate float numbers
            args[0] => script hash of the contract to be registered (scheduled)
            args[1] => how frequent the contract should run
            args[2] => the owner wants the contract to run with priority? if yes, the parameter receives GAS currency
             */
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(owner);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "register") return Register(args);
                else if (operation == "execute") return Execute(args);
                else if (operation == "cancel") return Cancel(args);
            }
            Runtime.Notify("No operation found!");
            return false;
        }

        static bool Execute(object[] args)
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
                Runtime.Notify("Its not time for contract execution yet.");
                return false;
            }

            //contract execution
            var contractParams = (object[])entry[0];
            var contractToBeInvoked = (AnotherContract)contract.Script.ToDelegate();
            var result = (bool)contractToBeInvoked((string)contractParams[0], (object[])contractParams[1]);
            if(!result)
            {  
               Runtime.Notify("Error in contract execution.");
               return false; 
            }

            entry[2] = timeStamp;
            Storage.Put(Storage.CurrentContext, contract.Script, entry.Serialize());
            return true;
        }

        static bool Register(object[] args)
        {
            object[] newEntry = new object[3];

            var contract = Blockchain.GetContract((byte[])args[0]);
            if (contract.Script == null)
            {
                Runtime.Notify("The given scripthash is invalid.");
                return false;
            }
            else if (Storage.Get(Storage.CurrentContext, contract.Script) != null)
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

            var contractParams = (object[])args[3];

            newEntry[0] = contractParams;
            newEntry[1] = frequency;
            newEntry[2] = 0; //last execution

            Storage.Put(Storage.CurrentContext, contract.Script, newEntry.Serialize());
            return true;
        }
        static bool Cancel(object[] args)
        {
            return false;
        }
    }
}