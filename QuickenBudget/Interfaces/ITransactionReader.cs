/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System.Collections.Generic;

using QuickenBudget.Models;

namespace QuickenBudget.Interfaces;

public interface ITransactionReader
{
    List<Transaction> Ingest();
}