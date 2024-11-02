# HomeAutomationScripting
Скриптовый сервис для управления умным домом.
Позволяет создавать JS-скрипты, которые будут что-то делать для автоматизации умного дома.
Например, когда в MQTT поступит определенное сообщение, можно будет что-то сделать еще.

На данный момент поддерживает два объекта внутри JS-скриптов:
MQTT - объект для связи с MQTT-сервером
MqttGroupSwitch - объект для создания групп ZigBee-выключателей, работающих под управлением ZigBee2MQTT. Условие - у выключателей должна быть включена опция **State action**. Выключатели, объединенные в группу работают все вместе на включение и выключение. Если один выключатель из группы включается, объект MqttGroupSwitch включит остальные выключатели из группы и наоборот, если один из выключателей внутри группы выключается, объект выключит все выключатели в группе.

Below is the machine translation:

Script service for managing a smart home.
Allows you to create JS scripts that will do something to automate a smart home.
For example, when a certain message arrives in MQTT, something else can be done.

Currently supports two objects inside JS scripts:
MQTT - object for communication with the MQTT server
MqttGroupSwitch - an object for creating groups of ZigBee switches running under ZigBee2MQTT control. Condition: the switches must have the **State action** option enabled. Switches combined into a group work together to turn on and off. If one switch from a group turns on, the MqttGroupSwitch object will turn on the rest of the switches in the group and vice versa, if one of the switches within the group turns off, the object will turn off all switches in the group.

# MqttGroupSwitch API:

## MqttGroupSwitch type:
* void Init(MQTTClient object) - initialize MqttGroupSwitch object with MQTT-client.
* SwitchGroup RegisterSwitchGroup() - create new switch-group object instance and return it

## SwitchGroup type:
* void AddSwitch(string switchType, string mqttTopicPrefix, string switchId, string buttonName) - add new switch in group. **switchType** - type of the switch: _Tuya_, _Aqara_ or _Shelly_. **mqttTopicPrefix** - prefix of the MQTT-topic of the switch. **switchId** - MAC-id of the switch. **buttonName** - name of the switch's button. _Remark_: if a switch-button does not have name (for ex. one-button Aqara switches) not use the third parameter.
* void Run() - run group

## Full example of use this API below:
```
MqttGroupSwitch.Init(MQTT);
let group = MqttGroupSwitch.RegisterSwitchGroup();
group.AddSwitch("Aqara", "zigbee2mqtt", "0x92857749820e23df"); // this is a button of the Aqara one-button switch
group.AddSwitch("Tuya", "zigbee2mqtt", "0x653528905af6759c", "center"); // this is a center button ot the Tuya three-button switch
group.AddSwitch("Aqara", "zigbee2mqtt", "0x8935af65c8b94352", "right"); // this is a right button of the Aqara two-button switch
group.AddSwitch("Shelly", "shellies", "shellypro2-ab78e5631a", "switch:1"); // this is a channel 1 of the Shelly two-channel relay 
group.Run();
```
