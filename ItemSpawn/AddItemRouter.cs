using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using System;

namespace Oracle.ItemSpawn
{
    //路由层客户端通信协议
    [Serializable]
    public class OracleAddCommand : EFT.InventoryLogic.Operations.CommandWithOwners
    {
        //路由请求类型
        [JsonProperty("Action")]
        public string Action = "OracleAdd";

        //物品数据
        [JsonProperty("ItemData")]
        public JsonType.FlatItem[] ItemData;
    }

    //行为描述
    public class OracleAddDescriptor : InventoryOperationDescriptor
    {
        public Item ItemData;

        public override Diz.LanguageExtensions.OperationCreationResult<EFT.InventoryLogic.Operations.AbstractOperation> ToInventoryOperation(IPlayer player)
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
    public class OracleAddOperationClass : EFT.InventoryLogic.Operations.AbstractOperation
    {
        private Item _itemToSpawn;

        //构造函数
        public OracleAddOperationClass(
        ushort id,
        ItemController controller,
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
        public override EFT.InventoryOperationDescriptor ToDescriptor()
        {
            return new OracleAddDescriptor
            {
                Operation = this,
                ItemData = _itemToSpawn
            };
        }

        //传递数据
        public override EFT.InventoryLogic.Operations.BaseInventoryCommand ToBaseInventoryCommand(string ownerId)
        {
            var itemFactory = Singleton<EFT.ItemFactory>.Instance;
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