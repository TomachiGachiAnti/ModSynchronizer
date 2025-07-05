// Description: 個体マターからアイテムを作成するレシピ


// 定数を定義:matterは個体マターのアイテム
val matter = <ic2:misc_resource:3>;

// オークの原木
recipes.addShaped(<minecraft:log:0> * 8, [
    [null, matter, null],
    [null, null, null],
    [null, null, null]
]);

// 雪ブロック
recipes.addShaped(<minecraft:snow> * 4, [
    [matter, null, matter],
    [null, null, null],
    [null, null, null]
]);

// 石ブロック
recipes.addShaped(<minecraft:stone:0> * 16, [
    [null, null, null],
    [null, matter, null],
    [null, null, null]
]);

// 銅鉱石ブロック
recipes.addShaped(<ic2:resource:1> * 5, [
    [null, null, matter],
    [matter, null, matter],
    [null, null, null]
]);

// 黒曜石
recipes.addShaped(<minecraft:obsidian> * 12, [
    [matter, null, matter],
    [matter, null, matter],
    [null, null, null]
]);

// 草ブロック
recipes.addShaped(<minecraft:grass> * 16, [
    [null, null, null],
    [matter, null, null],
    [matter, null, null]
]);

// ツタ
recipes.addShaped(<minecraft:vine> * 24, [
    [matter, null, null],
    [matter, null, null],
    [matter, null, null]
]);

// ネザーラック
recipes.addShaped(<minecraft:netherrack> * 16, [
    [null, null, matter],
    [null, matter, null],
    [matter, null, null]
]);

// 骨
recipes.addShaped(<minecraft:bone> * 32, [
    [matter, null, null],
    [matter, matter, null],
    [matter, null, null]
]);

// 模様入りの石レンガ
recipes.addShaped(<minecraft:stonebrick:3> * 48, [
    [matter, matter, null],
    [matter, matter, null],
    [matter, null, null]
]);

// 白色の羊毛ブロック
recipes.addShaped(<minecraft:wool:0> * 12, [
    [matter, null, matter],
    [null, null, null],
    [null, matter, null]
]);

// 水ブロック
recipes.addShaped(<ic2:misc_resource:7>, [
    [null, null, null],
    [null, matter, null],
    [null, matter, null]
]);

// 溶岩ブロック
recipes.addShaped(<ic2:misc_resource:8>, [
    [null, matter, null],
    [null, matter, null],
    [null, matter, null]
]);

// 砂岩
recipes.addShaped(<minecraft:sandstone:0> * 16, [
    [null, null, null],
    [null, null, matter],
    [null, matter, null]
]);

// ガラス
recipes.addShaped(<minecraft:glass> * 32, [
    [null, matter, null],
    [matter, null, matter],
    [null, matter, null]
]);

// イカスミ
recipes.addShaped(<minecraft:dye:0> * 48, [
    [null, matter, matter],
    [null, matter, matter],
    [null, matter, null]
]);

// 金鉱石ブロック
recipes.addShaped(<minecraft:gold_ore> * 2, [
    [null, matter, null],
    [matter, matter, matter],
    [null, matter, null]
]);

// 粘土
recipes.addShaped(<minecraft:clay_ball> * 48, [
    [matter, matter, null],
    [matter, null, null],
    [matter, matter, null]
]);

// 火打ち石
recipes.addShaped(<minecraft:flint> * 32, [
    [null, matter, null],
    [matter, matter, null],
    [matter, matter, null]
]);

// カカオ豆
recipes.addShaped(<minecraft:dye:3> * 32, [
    [matter, matter, null],
    [null, null, matter],
    [matter, matter, null]
]);

// 石炭
recipes.addShaped(<minecraft:coal:0> * 20, [
    [null, null, matter],
    [matter, null, null],
    [null, null, matter]
]);

// 錫鉱石ブロック
recipes.addShaped(<ic2:resource:3> * 5, [
    [null, null, null],
    [matter, null, matter],
    [null, null, matter]
]);

// 粘性樹脂
recipes.addShaped(<ic2:misc_resource:4> * 21, [
    [matter, null, matter],
    [null, null, null],
    [matter, null, matter]
]);

// 苔むした丸石
recipes.addShaped(<minecraft:mossy_cobblestone> * 16, [
    [null, null, null],
    [null, matter, null],
    [matter, null, matter]
]);

// 羽
recipes.addShaped(<minecraft:feather> * 32, [
    [null, matter, null],
    [null, matter, null],
    [matter, null, matter]
]);

// 鉄鉱石ブロック
recipes.addShaped(<minecraft:iron_ore> * 2, [
    [matter, null, matter],
    [null, matter, null],
    [matter, null, matter]
]);

// サトウキビ
recipes.addShaped(<minecraft:reeds> * 48, [
    [matter, null, matter],
    [matter, null, matter],
    [matter, null, matter]
]);

// サボテン
recipes.addShaped(<minecraft:cactus> * 48, [
    [null, matter, null],
    [matter, matter, matter],
    [matter, null, matter]
]);

// ラピスラズリ
recipes.addShaped(<minecraft:dye:4> * 9, [
    [null, matter, null],
    [null, matter, null],
    [null, matter, matter]
]);

// 雪玉
recipes.addShaped(<minecraft:snowball> * 16, [
    [null, null, null],
    [null, null, null],
    [matter, matter, matter]
]);

// 火薬
recipes.addShaped(<minecraft:gunpowder> * 15, [
    [matter, matter, matter],
    [matter, null, null],
    [matter, matter, matter]
]);

// レッドストーン
recipes.addShaped(<minecraft:redstone> * 24, [
    [null, null, null],
    [null, matter, null],
    [matter, matter, matter]
]);

// イリジウム鉱石
recipes.addShaped(<ic2:misc_resource:1>, [
    [matter, matter, matter],
    [null, matter, null],
    [matter, matter, matter]
]);

// 菌糸ブロック
recipes.addShaped(<minecraft:mycelium> * 24, [
    [null, null, null],
    [matter, null, matter],
    [matter, matter, matter]
]);

// グローストーン
recipes.addShaped(<minecraft:glowstone> * 8, [
    [null, matter, null],
    [matter, null, matter],
    [matter, matter, matter]
]);

// ダイヤモンド
recipes.addShaped(<minecraft:diamond>, [
    [matter, matter, matter],
    [matter, matter, matter],
    [matter, matter, matter]
]);