import { ProductSpecification } from '../models/product.model';

export const DEFAULT_SPEC_GROUP = 'General';

export const MAX_SPECIFICATIONS = 60;

export const MAX_SPEC_GROUP = 100;

export const MAX_SPEC_NAME = 100;

export const MAX_SPEC_VALUE = 500;

// Suggested section headings for the admin form.
export const COMMON_SPEC_GROUPS = [
  'General',
  'Display',
  'Performance',
  'Memory & Storage',
  'Camera',
  'Battery',
  'Connectivity',
  'Dimensions',
  'Warranty'
];

export interface SpecificationGroup {
  group: string;
  items: ProductSpecification[];
}

// Rows with neither a name nor a value are blank and ignored.
export function isBlankSpecification(spec: ProductSpecification): boolean {
  return !spec.name.trim() && !spec.value.trim();
}

// Message for one row, or an empty string when the row is fine.
export function specificationError(spec: ProductSpecification): string {

  if (isBlankSpecification(spec)) {
    return '';
  }

  if (!spec.name.trim()) {
    return 'Enter a name for this specification.';
  }

  if (!spec.value.trim()) {
    return 'Enter a value for this specification.';
  }

  return '';
}

// First problem in the whole list, including duplicates; empty when valid.
export function validateSpecifications(specs: ProductSpecification[]): string {

  const filled = specs.filter(spec => !isBlankSpecification(spec));
  const seen = new Set<string>();

  if (filled.length > MAX_SPECIFICATIONS) {
    return `A product can have at most ${MAX_SPECIFICATIONS} specifications.`;
  }

  for (const spec of filled) {

    const error = specificationError(spec);

    if (error) {
      return error;
    }

    const key = `${normalizedGroup(spec).toLowerCase()}\u0001${spec.name.trim().toLowerCase()}`;

    if (seen.has(key)) {
      return `"${spec.name.trim()}" is listed twice under "${normalizedGroup(spec)}".`;
    }

    seen.add(key);
  }

  return '';
}

// Trimmed rows ready to send; blanks are dropped.
export function cleanSpecifications(specs: ProductSpecification[]): ProductSpecification[] {

  return specs
    .filter(spec => !isBlankSpecification(spec))
    .map(spec => ({
      group: normalizedGroup(spec),
      name: spec.name.trim(),
      value: spec.value.trim()
    }));
}

// Groups in the order they first appear, like a product page specification sheet.
export function groupSpecifications(specs: ProductSpecification[] | undefined): SpecificationGroup[] {

  const groups = new Map<string, ProductSpecification[]>();

  for (const spec of specs ?? []) {

    const group = normalizedGroup(spec);

    groups.set(group, [...(groups.get(group) ?? []), spec]);
  }

  return Array.from(groups, ([group, items]) => ({ group, items }));
}

function normalizedGroup(spec: ProductSpecification): string {
  return spec.group?.trim() || DEFAULT_SPEC_GROUP;
}
