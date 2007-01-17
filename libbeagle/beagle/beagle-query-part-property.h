/* -*- Mode: C; tab-width: 8; indent-tabs-mode: t; c-basic-offset: 8; -*- */

/*
 * beagle-query-part-property.h
 *
 * Copyright (C) 2005 Novell, Inc.
 *
 */

/*
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

#ifndef __BEAGLE_QUERY_PART_PROPERTY_H
#define __BEAGLE_QUERY_PART_PROPERTY_H

#include <glib.h>

#include "beagle-query-part.h"

#define BEAGLE_TYPE_QUERY_PART_PROPERTY            (beagle_query_part_property_get_type ())
#define BEAGLE_QUERY_PART_PROPERTY(obj)            (G_TYPE_CHECK_INSTANCE_CAST ((obj), BEAGLE_TYPE_QUERY_PART_PROPERTY, BeagleQueryPartProperty))
#define BEAGLE_QUERY_PART_PROPERTY_CLASS(klass)    (G_TYPE_CHECK_CLASS_CAST ((klass), BEAGLE_TYPE_QUERY_PART_PROPERTY, BeagleQueryPartPropertyClass))
#define BEAGLE_IS_QUERY_PART_PROPERTY(obj)         (G_TYPE_CHECK_INSTANCE_TYPE ((obj), BEAGLE_TYPE_QUERY_PART_PROPERTY))
#define BEAGLE_IS_QUERY_PART_PROPERTY_CLASS(klass) (G_TYPE_CHECK_CLASS_TYPE ((klass), BEAGLE_TYPE_QUERY_PART_PROPERTY))
#define BEAGLE_QUERY_PART_PROPERTY_GET_CLASS(obj)  (G_TYPE_INSTANCE_GET_CLASS ((obj), BEAGLE_TYPE_QUERY_PART_PROPERTY, BeagleQueryPartPropertyClass))

typedef struct _BeagleQueryPartProperty      BeagleQueryPartProperty;
typedef struct _BeagleQueryPartPropertyClass BeagleQueryPartPropertyClass;

struct _BeagleQueryPartProperty {
	BeagleQueryPart parent;
};

struct _BeagleQueryPartPropertyClass {
        BeagleQueryPartClass parent_class;
};

GType                    beagle_query_part_property_get_type    (void);
BeagleQueryPartProperty *beagle_query_part_property_new         (void);

void beagle_query_part_property_set_key           (BeagleQueryPartProperty *part, 
						   const char              *key);
void beagle_query_part_property_set_value         (BeagleQueryPartProperty *part, 
						   const char              *value);
void beagle_query_part_property_set_property_type (BeagleQueryPartProperty *part,
						   BeaglePropertyType      prop_type);
#endif /* __BEAGLE_QUERY_PART_PROPERTY_H */

