﻿// <auto-generated />
using System;
using BTCPayServer.Plugins.LNbank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    [DbContext(typeof(LNbankPluginDbContext))]
    partial class LNbankPluginDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("BTCPayServer.Plugins.LNbank")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .HasAnnotation("ProductVersion", "3.1.19")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.AccessKey", b =>
                {
                    b.Property<string>("Key")
                        .HasColumnType("text");

                    b.Property<string>("WalletId")
                        .HasColumnType("text");

                    b.HasKey("Key");

                    b.HasIndex("WalletId");

                    b.ToTable("AccessKeys");
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

                    b.Property<string>("WalletId")
                        .HasColumnType("text");

                    b.HasKey("TransactionId");

                    b.HasIndex("InvoiceId");

                    b.HasIndex("WalletId");

                    b.ToTable("Transactions");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", b =>
                {
                    b.Property<string>("WalletId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("UserId")
                        .HasColumnType("text");

                    b.HasKey("WalletId");

                    b.HasIndex("UserId");

                    b.ToTable("Wallets");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.AccessKey", b =>
                {
                    b.HasOne("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", "Wallet")
                        .WithMany("AccessKeys")
                        .HasForeignKey("WalletId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Transaction", b =>
                {
                    b.HasOne("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", "Wallet")
                        .WithMany("Transactions")
                        .HasForeignKey("WalletId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
