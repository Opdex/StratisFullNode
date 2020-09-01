﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using NSubstitute.Core;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.SourceChain;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class MaturedBlocksProviderTests
    {
        private readonly IDepositExtractor depositExtractor;

        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        private readonly IConsensusManager consensusManager;

        public MaturedBlocksProviderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.depositExtractor = Substitute.For<IDepositExtractor>();
            this.consensusManager = Substitute.For<IConsensusManager>();
        }

        [Fact]
        public void GetMaturedBlocksReturnsDeposits()
        {
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10, null, true);

            foreach (ChainedHeader chainedHeader in headers)
            {
                chainedHeader.Block = new Block(chainedHeader.Header);
            }

            var blocks = new List<ChainedHeaderBlock>(headers.Count);

            foreach (ChainedHeader chainedHeader in headers)
            {
                blocks.Add(new ChainedHeaderBlock(chainedHeader.Block, chainedHeader));
            }

            ChainedHeader tip = headers.Last();

            this.consensusManager.GetBlockData(Arg.Any<List<uint256>>()).Returns(delegate (CallInfo info)
            {
                var hashes = (List<uint256>)info[0];
                return hashes.Select((hash) => blocks.Single(x => x.ChainedHeader.HashBlock == hash)).ToArray();
            });

            IFederatedPegSettings federatedPegSettings = Substitute.For<IFederatedPegSettings>();
            federatedPegSettings.MinimumDepositConfirmations.Returns(0);

            var deposits = new List<IDeposit>() { new Deposit(new uint256(0), DepositRetrievalType.Normal, 100, "test", 0, new uint256(1)) };

            this.depositExtractor.ExtractBlockDeposits(blocks.First(), DepositRetrievalType.Normal).ReturnsForAnyArgs(new MaturedBlockDepositsModel(new MaturedBlockInfoModel(), deposits));
            this.consensusManager.Tip.Returns(tip);

            // Makes every block a matured block.
            var maturedBlocksProvider = new MaturedBlocksProvider(this.consensusManager, this.depositExtractor, federatedPegSettings, this.loggerFactory);

            SerializableResult<List<MaturedBlockDepositsModel>> depositsResult = maturedBlocksProvider.RetrieveDeposits(DepositRetrievalType.Normal, 0);

            // Expect the number of matured deposits to equal the number of blocks.
            Assert.Equal(11, depositsResult.Value.Count);
        }
    }
}