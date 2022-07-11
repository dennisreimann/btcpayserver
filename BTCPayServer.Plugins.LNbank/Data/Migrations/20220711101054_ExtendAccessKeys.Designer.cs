﻿// <auto-generated />
using System;
using BTCPayServer.Plugins.LNbank.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    [DbContext(typeof(LNbankPluginDbContext))]
    [Migration("20220711101054_ExtendAccessKeys")]
    partial class ExtendAccessKeys
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("BTCPayServer.Plugins.LNbank")
                .HasAnnotation("ProductVersion", "6.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.AccessKey", b =>
                {
                    b.Property<string>("Key")
                        .HasColumnType("text");

                    b.Property<string>("Level")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text")
                        .HasDefaultValue("Admin");

                    b.Property<string>("UserId")
                        .HasColumnType("text");

                    b.Property<string>("WalletId")
                        .HasColumnType("text");

                    b.HasKey("Key");

                    b.HasIndex("WalletId");

                    b.ToTable("AccessKeys", "BTCPayServer.Plugins.LNbank");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Transaction", b =>
                {
                    b.Property<string>("TransactionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<long>("Amount")
                        .HasColumnType("bigint");

                    b.Property<long?>("AmountSettled")
                        .HasColumnType("bigint");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ExplicitStatus")
                        .HasColumnType("text");

                    b.Property<string>("InvoiceId")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset?>("PaidAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("PaymentRequest")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long?>("RoutingFee")
                        .HasColumnType("bigint");

                    b.Property<string>("WalletId")
                        .HasColumnType("text");

                    b.HasKey("TransactionId");

                    b.HasIndex("InvoiceId");

                    b.HasIndex("WalletId");

                    b.ToTable("Transactions", "BTCPayServer.Plugins.LNbank");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", b =>
                {
                    b.Property<string>("WalletId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsSoftDeleted")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("UserId")
                        .HasColumnType("text");

                    b.HasKey("WalletId");

                    b.HasIndex("UserId");

                    b.ToTable("Wallets", "BTCPayServer.Plugins.LNbank");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.AccessKey", b =>
                {
                    b.HasOne("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", "Wallet")
                        .WithMany("AccessKeys")
                        .HasForeignKey("WalletId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("Wallet");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Transaction", b =>
                {
                    b.HasOne("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", "Wallet")
                        .WithMany("Transactions")
                        .HasForeignKey("WalletId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("Wallet");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", b =>
                {
                    b.Navigation("AccessKeys");

                    b.Navigation("Transactions");
                });
#pragma warning restore 612, 618
        }
    }
}
