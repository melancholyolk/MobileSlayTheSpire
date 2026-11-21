// One-time starter workbook authoring. Subsequent balance edits belong in Cards.xlsx.
import fs from 'node:fs/promises';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';
const wb = Workbook.create();
const s = wb.worksheets.add('Cards');
const rows = [
 ['Id','Name','Class','Cost','Effect','Amount','Hits','Exhaust','StarterCopies'],
 ['w_strike','Iron Strike','Warrior',1,'Attack',7,1,false,5],
 ['w_guard','Iron Guard','Warrior',1,'Block',6,1,false,4],
 ['w_cleave','Cleave','Warrior',2,'Attack',18,1,false,1],
 ['w_rage','Battle Rage','Warrior',1,'Strength',3,1,true,0],
 ['w_flurry','Twin Blades','Warrior',1,'Attack',4,2,false,0],
 ['w_wall','Bulwark','Warrior',2,'Block',18,1,false,0],
 ['w_breath','Second Wind','Warrior',1,'Heal',10,1,true,0],
 ['w_focus','Resolve','Warrior',0,'Draw',2,1,true,0],
 ['h_shot','Quick Shot','Hunter',1,'Attack',6,1,false,5],
 ['h_dodge','Evasion','Hunter',1,'Block',6,1,false,4],
 ['h_venom','Venom Arrow','Hunter',1,'Poison',6,1,false,1],
 ['h_volley','Triple Shot','Hunter',1,'Attack',3,3,false,0],
 ['h_focus','Tracking','Hunter',0,'Draw',2,1,true,0],
 ['h_toxin','Deadly Toxin','Hunter',2,'Poison',12,1,true,0],
 ['h_shroud','Smoke Veil','Hunter',2,'Block',17,1,false,0],
 ['h_haste','Adrenaline','Hunter',0,'Energy',2,1,true,0]
];
s.getRange('A1:I17').values = rows;
s.showGridLines = false;
s.freezePanes.freezeRows(1);
s.getRange('A1:I17').format.font = {name:'Arial',size:11};
s.getRange('A1:I1').format = {fill:'#352F3A',font:{bold:true,color:'#FFFFFF'},rowHeight:30};
s.getRange('A2:I17').format.rowHeight = 24;
s.getRange('A1:I17').format.autofitColumns();
s.getRange('A1:B17').format.columnWidth = 20;
s.getRange('C1:I17').format.columnWidth = 16;
s.getRange('C2:C1000').dataValidation={rule:{type:'list',values:['Warrior','Hunter']}};
s.getRange('E2:E1000').dataValidation={rule:{type:'list',values:['Attack','Block','Strength','Poison','Draw','Heal','Energy']}};
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'Cards!A1:I17',tableMaxRows:18,tableMaxCols:9})).ndjson);
await fs.mkdir('GameData/Excel',{recursive:true});
await (await SpreadsheetFile.exportXlsx(wb)).save('GameData/Excel/Cards.xlsx');
await fs.mkdir('.uloop/table-preview',{recursive:true});
const png=await wb.render({sheetName:'Cards',range:'A1:I17',scale:1,format:'png'});
await fs.writeFile('.uloop/table-preview/cards.png',new Uint8Array(await png.arrayBuffer()));
