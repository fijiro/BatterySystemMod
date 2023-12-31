import { BotGeneratorHelper } from "../helpers/BotGeneratorHelper";
import { BotWeaponGeneratorHelper } from "../helpers/BotWeaponGeneratorHelper";
import { HandbookHelper } from "../helpers/HandbookHelper";
import { ItemHelper } from "../helpers/ItemHelper";
import { Inventory as PmcInventory } from "../models/eft/common/tables/IBotBase";
import { IBotType, Inventory, ModsChances } from "../models/eft/common/tables/IBotType";
import { Item } from "../models/eft/common/tables/IItem";
import { ITemplateItem } from "../models/eft/common/tables/ITemplateItem";
import { IBotConfig } from "../models/spt/config/IBotConfig";
import { ILogger } from "../models/spt/utils/ILogger";
import { ConfigServer } from "../servers/ConfigServer";
import { DatabaseServer } from "../servers/DatabaseServer";
import { BotLootCacheService } from "../services/BotLootCacheService";
import { LocalisationService } from "../services/LocalisationService";
import { HashUtil } from "../utils/HashUtil";
import { RandomUtil } from "../utils/RandomUtil";
import { BotWeaponGenerator } from "./BotWeaponGenerator";
export declare class BotLootGenerator {
    protected logger: ILogger;
    protected hashUtil: HashUtil;
    protected randomUtil: RandomUtil;
    protected itemHelper: ItemHelper;
    protected databaseServer: DatabaseServer;
    protected handbookHelper: HandbookHelper;
    protected botGeneratorHelper: BotGeneratorHelper;
    protected botWeaponGenerator: BotWeaponGenerator;
    protected botWeaponGeneratorHelper: BotWeaponGeneratorHelper;
    protected botLootCacheService: BotLootCacheService;
    protected localisationService: LocalisationService;
    protected configServer: ConfigServer;
    protected botConfig: IBotConfig;
    constructor(logger: ILogger, hashUtil: HashUtil, randomUtil: RandomUtil, itemHelper: ItemHelper, databaseServer: DatabaseServer, handbookHelper: HandbookHelper, botGeneratorHelper: BotGeneratorHelper, botWeaponGenerator: BotWeaponGenerator, botWeaponGeneratorHelper: BotWeaponGeneratorHelper, botLootCacheService: BotLootCacheService, localisationService: LocalisationService, configServer: ConfigServer);
    /**
     * Add loot to bots containers
     * @param sessionId Session id
     * @param botJsonTemplate Base json db file for the bot having its loot generated
     * @param isPmc Will bot be a pmc
     * @param botRole Role of bot, e.g. asssult
     * @param botInventory Inventory to add loot to
     * @param botLevel Level of bot
     */
    generateLoot(sessionId: string, botJsonTemplate: IBotType, isPmc: boolean, botRole: string, botInventory: PmcInventory, botLevel: number): void;
    /**
     * Force healing items onto bot to ensure they can heal in-raid
     * @param botInventory Inventory to add items to
     * @param botRole Role of bot (sptBear/sptUsec)
     */
    protected addForcedMedicalItemsToPmcSecure(botInventory: PmcInventory, botRole: string): void;
    protected getRandomisedCount(min: number, max: number, nValue: number): number;
    /**
     * Take random items from a pool and add to an inventory until totalItemCount or totalValueLimit is reached
     * @param pool pool of items to pick from
     * @param equipmentSlots What equality slot will the loot items be added to
     * @param totalItemCount Max count of items to add
     * @param inventoryToAddItemsTo bot inventory loot will be added to
     * @param botRole role of the bot loot is being generated for (assault/pmcbot)
     * @param useLimits should item limit counts be used as defined in config/bot.json
     * @param totalValueLimitRub total value of loot allowed in roubles
     * @param isPmc is the bot being generated for a pmc
     */
    protected addLootFromPool(pool: ITemplateItem[], equipmentSlots: string[], totalItemCount: number, inventoryToAddItemsTo: PmcInventory, botRole: string, useLimits?: boolean, totalValueLimitRub?: number, isPmc?: boolean): void;
    /**
     * Add generated weapons to inventory as loot
     * @param botInventory inventory to add preset to
     * @param equipmentSlot slot to place the preset in (backpack)
     * @param templateInventory bots template, assault.json
     * @param modChances chances for mods to spawn on weapon
     * @param botRole bots role, .e.g. pmcBot
     * @param isPmc are we generating for a pmc
     */
    protected addLooseWeaponsToInventorySlot(sessionId: string, botInventory: PmcInventory, equipmentSlot: string, templateInventory: Inventory, modChances: ModsChances, botRole: string, isPmc: boolean, botLevel: number): void;
    /**
     * @deprecated replaced by getRandomItemFromPoolByRole()
     * Get a random item from the pool parameter using the biasedRandomNumber system
     * @param pool pool of items to pick an item from
     * @param isPmc is the bot being created a pmc
     * @returns ITemplateItem object
     */
    protected getRandomItemFromPool(pool: ITemplateItem[], isPmc: boolean): ITemplateItem;
    /**
     * Get a random item from the pool parameter using the biasedRandomNumber system
     * @param pool pool of items to pick an item from
     * @param isPmc is the bot being created a pmc
     * @returns ITemplateItem object
     */
    protected getRandomItemFromPoolByRole(pool: ITemplateItem[], botRole: string): ITemplateItem;
    /**
     * @deprecated Replaced by getBotLootNValueByRole()
     * Get the loot nvalue from botconfig
     * @param isPmc if true the pmc nvalue is returned
     * @returns nvalue as number
     */
    protected getBotLootNValue(isPmc: boolean): number;
    /**
     * Get the loot nvalue from botconfig
     * @param botRole role of bot e.g. assault/sptBear
     * @returns nvalue as number
     */
    protected getBotLootNValueByRole(botRole: string): number;
    /**
     * Update item limit array to contain items that have a limit
     * All values are set to 0
     * @param isPmc is the bot a pmc
     * @param botRole role the bot has
     * @param limitCount
     */
    protected initItemLimitArray(isPmc: boolean, botRole: string, limitCount: Record<string, number>): void;
    /**
     * Check if an item has reached its bot-specific spawn limit
     * @param itemTemplate Item we check to see if its reached spawn limit
     * @param botRole Bot type
     * @param isPmc Is bot we're working with a pmc
     * @param limitCount spawn limits for items on bot
     * @param itemSpawnLimits the limits this bot is allowed to have
     * @returns true if item has reached spawn limit
     */
    protected itemHasReachedSpawnLimit(itemTemplate: ITemplateItem, botRole: string, isPmc: boolean, limitCount: Record<string, number>, itemSpawnLimits: Record<string, number>): boolean;
    /**
     * Randomise the stack size of a money object, uses different values for pmc or scavs
     * @param isPmc is this a PMC
     * @param itemTemplate item details
     * @param moneyItem Money stack to randomise
     */
    protected randomiseMoneyStackSize(isPmc: boolean, itemTemplate: ITemplateItem, moneyItem: Item): void;
    /**
     * Randomise the size of an ammo stack
     * @param isPmc is this a PMC
     * @param itemTemplate item details
     * @param ammoItem Ammo stack to randomise
     */
    protected randomiseAmmoStackSize(isPmc: boolean, itemTemplate: ITemplateItem, ammoItem: Item): void;
    /**
     * Get spawn limits for a specific bot type from bot.json config
     * If no limit found for a non pmc bot, fall back to defaults
     * @param isPmc is the bot we want limits for a pmc
     * @param botRole what role does the bot have
     * @returns Dictionary of tplIds and limit
     */
    protected getItemSpawnLimitsForBotType(isPmc: boolean, botRole: string): Record<string, number>;
    /**
     * Get the parentId or tplId of item inside spawnLimits object if it exists
     * @param itemTemplate item we want to look for in spawn limits
     * @param spawnLimits Limits to check for item
     * @returns id as string, otherwise undefined
     */
    protected getMatchingIdFromSpawnLimits(itemTemplate: ITemplateItem, spawnLimits: Record<string, number>): string;
}
