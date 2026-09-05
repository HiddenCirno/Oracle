using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using System;

namespace Oracle.ItemSpawn
{
    //路由层客户端通信协议
    [Serializable]
    public class OracleAddCommand : GClass3473
    {
        //路由请求类型
        [JsonProperty("Action")]
        public string Action = "SyncStashExtend";

        //物品数据
        [JsonProperty("stashData")]
        public FlatItemsDataClass[] ItemData;
    }

    //行为描述
    public class OracleAddDescriptor : BaseDescriptorClass
    {
        public Item ItemData;

        public override GStruct152<BaseInventoryOperationClass> ToInventoryOperation(IPlayer player)
        {
            var operation = new OracleAddOperationClass(
                OperationId,
                player.InventoryController,
                ItemData
            );
            return operation;
        }
    }

    //行为执行体
    public class OracleAddOperationClass : BaseInventoryOperationClass
    {
        private Item _itemToSpawn;

        //构造函数
        public OracleAddOperationClass(
        ushort id,
        TraderControllerClass controller,
        Item item)
        : base(id, controller)
        {
            _itemToSpawn = item;
        }

        public override void ExecuteInternal(Callback callback)
        {
            callback?.Invoke(SuccessfulResult.New);
        }

        //描述
        public override BaseDescriptorClass ToDescriptor()
        {
            return new OracleAddDescriptor
            {
                Operation = this,
                ItemData = _itemToSpawn
            };
        }

        //传递数据
        public override GClass3471 ToBaseInventoryCommand(string ownerId)
        {
            var itemFactory = Singleton<ItemFactoryClass>.Instance;
            return new OracleAddCommand
            {
                ItemData = itemFactory.TreeToFlatItems(new Item[] { _itemToSpawn })
            };
        }

        //回收
        public override void Dispose()
        {
        }
    }
}