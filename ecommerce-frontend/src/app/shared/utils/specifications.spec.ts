import {
  cleanSpecifications,
  groupSpecifications,
  specificationError,
  validateSpecifications
} from './specifications';

const spec = (group: string | null, name: string, value: string) => ({ group, name, value });

describe('specification utils', () => {

  it('treats rows with neither name nor value as blank and valid', () => {
    expect(specificationError(spec('', '  ', ''))).toBe('');
    expect(validateSpecifications([spec(null, '', ''), spec('Display', 'Size', '6 inch')])).toBe('');
  });

  it('asks for the missing half of a row', () => {
    expect(specificationError(spec(null, '', 'x'))).toContain('name');
    expect(specificationError(spec(null, 'Brand', ' '))).toContain('value');
  });

  it('rejects the same name twice in one group, ignoring case', () => {
    expect(validateSpecifications([spec('Display', 'Size', '6'), spec('display', 'SIZE', '7')])).toContain('twice');
  });

  it('allows the same name in different groups', () => {
    expect(validateSpecifications([spec('Front', 'Camera', '8 MP'), spec('Rear', 'Camera', '48 MP')])).toBe('');
  });

  it('limits a product to 60 specifications', () => {
    const many = Array.from({ length: 61 }, (_, i) => spec(null, `Name ${i}`, 'v'));

    expect(validateSpecifications(many)).toContain('60');
  });

  it('trims rows, defaults the group and drops blanks before sending', () => {
    expect(cleanSpecifications([spec(' ', ' Brand ', ' Acme '), spec(null, '', '')])).toEqual([
      { group: 'General', name: 'Brand', value: 'Acme' }
    ]);
  });

  it('groups in order of first appearance', () => {
    const groups = groupSpecifications([
      spec('Display', 'Size', '6'),
      spec(null, 'Brand', 'Acme'),
      spec('Display', 'Resolution', 'FHD')
    ]);

    expect(groups.map(g => g.group)).toEqual(['Display', 'General']);
    expect(groups[0].items.map(i => i.name)).toEqual(['Size', 'Resolution']);
    expect(groupSpecifications(undefined)).toEqual([]);
  });
});
