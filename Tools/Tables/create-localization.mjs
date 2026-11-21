// One-time seed authoring only. Translations are maintained in the workbook after creation.
import fs from 'node:fs/promises';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';
const rows=[['Key','Comment','en','zh-Hans'],
['ui.title','Game title','ASHEN ASCENT','灰烬之巅'],
['ui.subtitle','Main menu','A cooperative deckbuilding prototype','合作卡牌冒险原型'],
['ui.menu','Navigation','Menu','菜单'],
['ui.choose','Class selection','Choose your path','选择你的道路'],
['ui.warrior','Class button','WARRIOR\nStrength & steel','战士\n力量与钢铁'],
['ui.hunter','Class button','HUNTER\nPoison & precision','猎人\n毒素与精准'],
['ui.host','Network button','Host co-op\nPlay as Warrior','创建联机\n扮演战士'],
['ui.join','Network button','Join co-op\nPlay as Hunter','加入联机\n扮演猎人'],
['ui.end','Combat button','End turn','结束回合'],
['ui.wait','Combat button','Waiting...','等待中……'],
['ui.skip','Reward button','Skip','跳过'],
['ui.reward','Reward title','Victory — choose one card, or skip.','战斗胜利——选择一张卡牌，或跳过。'],
['ui.win','Run ending','THE WARDEN FALLS','守望者倒下了'],
['ui.lose','Run ending','THE ASCENT ENDS','攀登之旅结束'],
['ui.restart','Run ending','Return to the menu to begin a new run.','返回菜单，开始新的冒险。'],
['ui.reconnect','Network button','Reconnect','重新连接'],
['card.w_strike.name','Card name','Iron Strike','铁刃打击'],
['card.w_guard.name','Card name','Iron Guard','铁壁防御'],
['card.w_cleave.name','Card name','Cleave','横扫'],
['card.w_rage.name','Card name','Battle Rage','战斗怒火'],
['card.w_flurry.name','Card name','Twin Blades','双刃连击'],
['card.w_wall.name','Card name','Bulwark','坚壁'],
['card.w_breath.name','Card name','Second Wind','重整旗鼓'],
['card.w_focus.name','Card name','Resolve','决心'],
['card.h_shot.name','Card name','Quick Shot','快射'],
['card.h_dodge.name','Card name','Evasion','闪避'],
['card.h_venom.name','Card name','Venom Arrow','毒箭'],
['card.h_volley.name','Card name','Triple Shot','三连射'],
['card.h_focus.name','Card name','Tracking','追踪'],
['card.h_toxin.name','Card name','Deadly Toxin','致命毒素'],
['card.h_shroud.name','Card name','Smoke Veil','烟幕'],
['card.h_haste.name','Card name','Adrenaline','肾上腺素']];
const target='GameData/Excel/Localization/Game.xlsx';
try { await fs.access(target); throw new Error('Refusing to overwrite developer translations'); } catch(e) { if(e.code!=='ENOENT') throw e; }
const wb=Workbook.create(); const sheet=wb.worksheets.add('Strings');
sheet.getRange(`A1:D${rows.length}`).values=rows;
sheet.freezePanes.freezeRows(1); sheet.showGridLines=false;
sheet.getRange(`A1:D${rows.length}`).format.font={name:'Microsoft YaHei',size:11};
sheet.getRange('A1:D1').format={fill:'#352F3A',font:{bold:true,color:'#FFFFFF'},rowHeight:30};
sheet.getRange(`A2:D${rows.length}`).format.rowHeight=38;
sheet.getRange(`A1:B${rows.length}`).format.columnWidth=27;
sheet.getRange(`C1:D${rows.length}`).format.columnWidth=52;
await fs.mkdir('GameData/Excel/Localization',{recursive:true});
await (await SpreadsheetFile.exportXlsx(wb)).save(target);
await fs.mkdir('.uloop/table-preview',{recursive:true});
await fs.writeFile('.uloop/table-preview/localization.png',new Uint8Array(await (await wb.render({sheetName:'Strings',range:'A1:D10',format:'png',scale:1})).arrayBuffer()));
console.log(`Created ${rows.length-1} bilingual entries`);
