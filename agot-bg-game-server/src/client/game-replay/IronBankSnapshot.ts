import IIronBankSnapshot from "./IIronBankSnapshot";

export default class IronBankSnapshot implements IIronBankSnapshot {
  loanSlots: (string | null)[];
  interestCosts?: [string, number][];
  braavosController?: string;

  constructor(data: IIronBankSnapshot) {
    this.loanSlots = [...data.loanSlots];
    // Copy each [house, cost] tuple too, not just the outer array: Otherwise mutating
    // costsOfHouse[1] in-place (see SnapshotMigrator's "loan-purchased" case) would also
    // mutate the tuple of the snapshot this one was copied from (e.g. a cached seenSnapshot).
    this.interestCosts = data.interestCosts
      ? data.interestCosts.map(([house, cost]) => [house, cost])
      : undefined;
    this.braavosController = data.braavosController;
  }

  getCopy(): IronBankSnapshot {
    return new IronBankSnapshot(this);
  }
}
