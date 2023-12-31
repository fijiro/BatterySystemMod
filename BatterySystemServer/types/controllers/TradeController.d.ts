import { ItemHelper } from "../helpers/ItemHelper";
import { ProfileHelper } from "../helpers/ProfileHelper";
import { TradeHelper } from "../helpers/TradeHelper";
import { IPmcData } from "../models/eft/common/IPmcData";
import { Upd } from "../models/eft/common/tables/IItem";
import { IItemEventRouterResponse } from "../models/eft/itemEvent/IItemEventRouterResponse";
import { IProcessBaseTradeRequestData } from "../models/eft/trade/IProcessBaseTradeRequestData";
import { IProcessRagfairTradeRequestData } from "../models/eft/trade/IProcessRagfairTradeRequestData";
import { IRagfairConfig } from "../models/spt/config/IRagfairConfig";
import { ITraderConfig } from "../models/spt/config/ITraderConfig";
import { ILogger } from "../models/spt/utils/ILogger";
import { EventOutputHolder } from "../routers/EventOutputHolder";
import { ConfigServer } from "../servers/ConfigServer";
import { RagfairServer } from "../servers/RagfairServer";
import { LocalisationService } from "../services/LocalisationService";
import { HttpResponseUtil } from "../utils/HttpResponseUtil";
declare class TradeController {
    protected logger: ILogger;
    protected eventOutputHolder: EventOutputHolder;
    protected tradeHelper: TradeHelper;
    protected itemHelper: ItemHelper;
    protected profileHelper: ProfileHelper;
    protected ragfairServer: RagfairServer;
    protected httpResponse: HttpResponseUtil;
    protected localisationService: LocalisationService;
    protected configServer: ConfigServer;
    protected ragfairConfig: IRagfairConfig;
    protected traderConfig: ITraderConfig;
    constructor(logger: ILogger, eventOutputHolder: EventOutputHolder, tradeHelper: TradeHelper, itemHelper: ItemHelper, profileHelper: ProfileHelper, ragfairServer: RagfairServer, httpResponse: HttpResponseUtil, localisationService: LocalisationService, configServer: ConfigServer);
    /** Handle TradingConfirm event */
    confirmTrading(pmcData: IPmcData, request: IProcessBaseTradeRequestData, sessionID: string): IItemEventRouterResponse;
    /** Handle RagFairBuyOffer event */
    confirmRagfairTrading(pmcData: IPmcData, body: IProcessRagfairTradeRequestData, sessionID: string): IItemEventRouterResponse;
    protected confirmTradingInternal(pmcData: IPmcData, body: IProcessBaseTradeRequestData, sessionID: string, foundInRaid?: boolean, upd?: Upd): IItemEventRouterResponse;
}
export { TradeController };
