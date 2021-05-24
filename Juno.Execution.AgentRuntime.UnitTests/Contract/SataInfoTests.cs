namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using AutoFixture;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SataInfoTests : RuntimeContractsTests<SataInfo>
    {
        [Test]
        [TestCase(null)]
        public void ConstructorValidatesStringParameters(string invalidParameter)
        {
            SataInfo validComponent = this.MockFixture.Create<SataInfo>();
            Assert.Throws<ArgumentException>(() => new SataInfo(validComponent.Device, validComponent.SmartHealth, invalidParameter, validComponent.FirmwareVersion, validComponent.SerialNumber, validComponent.SmartAttributes));
            Assert.Throws<ArgumentException>(() => new SataInfo(validComponent.Device, validComponent.SmartHealth, validComponent.ModelName, invalidParameter, validComponent.SerialNumber, validComponent.SmartAttributes));
            Assert.Throws<ArgumentException>(() => new SataInfo(validComponent.Device, validComponent.SmartHealth, validComponent.ModelName, validComponent.FirmwareVersion, invalidParameter, validComponent.SmartAttributes));
        }

        [Test]
        public void ConstructorValidatesNonStringParameters()
        {
            SataInfo validComponent = this.MockFixture.Create<SataInfo>();
            Assert.Throws<ArgumentException>(() => new SataInfo(null, validComponent.SmartHealth, validComponent.ModelName, validComponent.FirmwareVersion, validComponent.SerialNumber, validComponent.SmartAttributes));
            Assert.Throws<ArgumentException>(() => new SataInfo(validComponent.Device, null, validComponent.ModelName, validComponent.FirmwareVersion, validComponent.SerialNumber, validComponent.SmartAttributes));
            Assert.Throws<ArgumentException>(() => new SataInfo(validComponent.Device, validComponent.SmartHealth, validComponent.ModelName, validComponent.FirmwareVersion, validComponent.SerialNumber, null));
        }

        [Test]
        public void SataInfoIsJsonDeserializiableFromExampleJsonString()
        {
            Assert.DoesNotThrow(() => JsonConvert.DeserializeObject<SataInfo>(AtaExampleOutput.ExampleOutput));
        }

        /// <summary>
        /// Need to test this output explicitly because 
        /// there is not local access to a SATA drive. 
        /// </summary>
        private class AtaExampleOutput
        {
            public const string ExampleOutput = @"
            {
                ""json_format_version"": [
                    1,
                    0
                ],
                ""smartctl"": {
                    ""version"": [
                        7,
                        1
                    ],
                    ""svn_revision"": ""4933"",
                    ""platform_info"": ""i686-w64-mingw32-2019(64)"",
                    ""build_info"": ""(CircleCI)"",
                    ""argv"": [
                        ""smartctl"",
                        ""-a"",
                        ""-j"",
                        ""/dev/sda""
                    ],
                    ""exit_status"": 0
                },
                ""device"": {
                    ""name"": ""/dev/sda"",
                    ""info_name"": ""/dev/sda"",
                    ""type"": ""ata"",
                    ""protocol"": ""ATA""
                },
                ""model_family"": ""Micron 5100 Pro / 5200 SSDs"",
                ""model_name"": ""Micron_5200_MTFDDAK960TDD"",
                ""serial_number"": ""18291DAF0FF7"",
                ""wwn"": {
                    ""naa"": 5,
                    ""oui"": 41077,
                    ""id"": 4792979447
                },
                ""firmware_version"": ""D1RL002"",
                ""user_capacity"": {
                    ""blocks"": 1875385008,
                    ""bytes"": 960197124096
                },
                ""logical_block_size"": 512,
                ""physical_block_size"": 4096,
                ""rotation_rate"": 0,
                ""form_factor"": {
                    ""ata_value"": 3,
                    ""name"": ""2.5 inches""
                },
                ""in_smartctl_database"": true,
                ""ata_version"": {
                    ""string"": ""ACS-3 T13/2161-D revision 5"",
                    ""major_value"": 2040,
                    ""minor_value"": 109
                },
                ""sata_version"": {
                    ""string"": ""SATA 3.2"",
                    ""value"": 255
                },
                ""interface_speed"": {
                    ""max"": {
                        ""sata_value"": 14,
                        ""string"": ""6.0 Gb/s"",
                        ""units_per_second"": 60,
                        ""bits_per_unit"": 100000000
                    },
                    ""current"": {
                        ""sata_value"": 3,
                        ""string"": ""6.0 Gb/s"",
                        ""units_per_second"": 60,
                        ""bits_per_unit"": 100000000
                    }
                },
                ""local_time"": {
                    ""time_t"": 1611865698,
                    ""asctime"": ""Thu Jan 28 12:28:18 2021 PST""
                },
                ""smart_status"": {
                    ""passed"": true
                },
                ""ata_smart_data"": {
                    ""offline_data_collection"": {
                        ""status"": {
                            ""value"": 0,
                            ""string"": ""was never started""
                        },
                        ""completion_seconds"": 2034
                    },
                    ""self_test"": {
                        ""status"": {
                            ""value"": 0,
                            ""string"": ""completed without error"",
                            ""passed"": true
                        },
                        ""polling_minutes"": {
                            ""short"": 2,
                            ""extended"": 8,
                            ""conveyance"": 3
                        }
                    },
                    ""capabilities"": {
                        ""values"": [
                            123,
                            3
                        ],
                        ""exec_offline_immediate_supported"": true,
                        ""offline_is_aborted_upon_new_cmd"": false,
                        ""offline_surface_scan_supported"": true,
                        ""self_tests_supported"": true,
                        ""conveyance_self_test_supported"": true,
                        ""selective_self_test_supported"": true,
                        ""attribute_autosave_enabled"": true,
                        ""error_logging_supported"": true,
                        ""gp_logging_supported"": true
                    }
                },
                ""ata_sct_capabilities"": {
                    ""value"": 53,
                    ""error_recovery_control_supported"": false,
                    ""feature_control_supported"": true,
                    ""data_table_supported"": true
                },
                ""ata_smart_attributes"": {
                    ""revision"": 16,
                    ""table"": [
                        {
                            ""id"": 1,
                            ""name"": ""Raw_Read_Error_Rate"",
                            ""value"": 100,
                            ""worst"": 100,
                            ""thresh"": 50,
                            ""when_failed"": """",
                            ""flags"": {
                                ""value"": 47,
                                ""string"": ""POSR-K "",
                                ""prefailure"": true,
                                ""updated_online"": true,
                                ""performance"": true,
                                ""error_rate"": true,
                                ""event_count"": false,
                                ""auto_keep"": true
                            },
                            ""raw"": {
                                ""value"": 0,
                                ""string"": ""0""
                            }
                        }	
                    ]
                },
                ""power_on_time"": {
                    ""hours"": 8531
                },
                ""power_cycle_count"": 523,
                ""temperature"": {
                    ""current"": 20
                },
                ""ata_smart_error_log"": {
                    ""summary"": {
                        ""revision"": 1,
                        ""count"": 0
                    }
                }
            }";
        }
    }
}
